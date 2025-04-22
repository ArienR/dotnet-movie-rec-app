using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Trainers;
using MovieRecApp.Server.Data;
using MovieRecApp.Server.Interfaces;
using MovieRecApp.Server.Models;

namespace MovieRecApp.Server.Services;

public class RecommendationService : IRecommendationService
{
    private const string MODEL_PATH = "model.zip";
    private readonly ApplicationDbContext _db;
    private readonly MLContext _mlContext;
    private ITransformer _model;
    private readonly ILetterboxdScraper _scraper;
    private static readonly object _retrainLock = new();
    private readonly HashSet<string> _scrapedThisScope = new();

    public RecommendationService(
        ApplicationDbContext db,
        ILetterboxdScraper scraper)
    {
        _db        = db;
        _mlContext = new MLContext();
        _scraper   = scraper;

        if (File.Exists(MODEL_PATH))
        {
            try
            {
                using var fs = File.OpenRead(MODEL_PATH);
                _model = _mlContext.Model.Load(fs, out _);
            }
            catch
            {
                _model = null; // bad file will be overwritten on retrain
            }
        }
    }

    public async Task RetrainModelAsync()
    {
        var trainingData = await _db.Ratings
            .Select(r => new RatingInput {
                UserName = r.UserName,
                MovieId  = r.MovieId,
                Score    = r.Score
            })
            .ToListAsync();

        var view = _mlContext.Data.LoadFromEnumerable(trainingData);

        var pipeline = _mlContext.Transforms
            .Conversion.MapValueToKey("userKey", nameof(RatingInput.UserName))
            .Append(_mlContext.Transforms
                .Conversion.MapValueToKey("movieKey", nameof(RatingInput.MovieId)))
            .Append(_mlContext.Recommendation()
                .Trainers.MatrixFactorization(new()
                {
                    MatrixColumnIndexColumnName = "userKey",
                    MatrixRowIndexColumnName    = "movieKey",
                    LabelColumnName             = "Label",
                    NumberOfIterations          = 20,
                    ApproximationRank           = 100
                }));
        
        // Avoid concurrent writes to the model
        lock (_retrainLock)
        {
            var model = pipeline.Fit(view);
            using var fs = File.Create(MODEL_PATH); // truncates existing
            _mlContext.Model.Save(model, view.Schema, fs);
            _model = model;
        }
    }
    
    public async Task<(Movie Movie, float Score)[]> GetTopRecommendationsWithMoviesAsync(
        string username, int count)
    {
        // --- A) Incremental scrape & retrain if new ratings exist ---
        var beforeCount = await _db.Ratings.CountAsync(r => r.UserName == username);
        await _scraper.ScrapeRatingsForUserAsync(username);
        var afterCount = await _db.Ratings.CountAsync(r => r.UserName == username);

        if (_model == null || afterCount > beforeCount)
        {
            lock (_retrainLock)
            {
                // we can call synchronously inside lock since RetrainModelAsync
                // only does CPU + IO and uses same lock internally
                RetrainModelAsync().GetAwaiter().GetResult();
            }
        }

        // In case retraining silently fails
        if (_model == null)
            throw new InvalidOperationException(
                "Model not trained. Call RetrainModelAsync first.");

        // Build a prediction engine once
        var engine = _mlContext.Model
            .CreatePredictionEngine<RatingInput, RatingPrediction>(_model);

        // Find all movies the user has rated
        var seenMovieIds = await _db.Ratings
            .Where(r => r.UserName == username)
            .Select(r => r.MovieId)
            .ToListAsync();

        // Load all movies and filter out seen ones
        var candidates = await _db.Movies
            .Where(m => !seenMovieIds.Contains(m.MovieId))
            .ToListAsync();

        // Score each and take the top N
        var scored = candidates
            .Select(m => (
                Movie: m,
                Score: engine.Predict(
                    new RatingInput { UserName = username, MovieId = m.MovieId }
                ).PredictedScore
            ))
            .Where(x => !float.IsInfinity(x.Score) && !float.IsNaN(x.Score))
            .OrderByDescending(x => x.Score)
            .Take(count)
            .ToArray();

        return scored;
    }

    public async Task<bool> HasRatingsAsync(string username)
    {
        return await _db.Ratings.AnyAsync(r => r.UserName == username);
    }

    public async Task EnsureUserHasRatingsAsync(string username)
    {
        if (!await HasRatingsAsync(username))
            throw new InvalidOperationException(
                $"User '{username}' has no ratings. Call ScrapeRatingsForUserAsync first."
            );
    }
}