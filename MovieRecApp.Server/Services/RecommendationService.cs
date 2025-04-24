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
    private readonly ILogger<RecommendationService> _logger;
    private readonly SemaphoreSlim _retrainLock = new(1, 1);

    public RecommendationService(
        ApplicationDbContext db,
        ILetterboxdScraper scraper,
        ILogger<RecommendationService> logger)
    {
        _db        = db;
        _mlContext = new MLContext();
        _scraper   = scraper;
        _logger    = logger;

        // Try to load existing model
        if (File.Exists(MODEL_PATH))
        {
            try
            {
                using var fs = File.OpenRead(MODEL_PATH);
                _model = _mlContext.Model.Load(fs, out _);
                _logger.LogInformation("Loaded existing model from {Path}", MODEL_PATH);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load model; will retrain on demand");
                _model = null;
            }
        }
    }

    public async Task RetrainModelAsync()
    {
        await _retrainLock.WaitAsync();
        try
        {
            _logger.LogInformation("Retraining recommendation model...");
            var trainingData = await _db.Ratings
                .Select(r => new RatingInput {
                    UserName = r.UserName,
                    MovieId  = r.MovieId,
                    Score    = r.Score
                })
                .ToListAsync();

            var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

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

            var model = pipeline.Fit(dataView);
            using var fs = File.Create(MODEL_PATH);
            _mlContext.Model.Save(model, dataView.Schema, fs);
            _model = model;
            _logger.LogInformation("Model retraining complete and saved to {Path}", MODEL_PATH);
        }
        finally
        {
            _retrainLock.Release();
        }
    }

    public async Task<(Movie Movie, float Score)[]> GetTopRecommendationsWithMoviesAsync(
        string username, int count)
    {
        // 1) Scrape for new ratings
        var before = await _db.Ratings.CountAsync(r => r.UserName == username);
        await _scraper.ScrapeRatingsForUserAsync(username);
        var after  = await _db.Ratings.CountAsync(r => r.UserName == username);

        // 2) Retrain if necessary
        if (_model == null || after > before)
        {
            _logger.LogInformation(
                "User {User} ratings changed (before={Before}, after={After}), retraining",
                username, before, after);
            await RetrainModelAsync();
        }

        if (_model == null)
            throw new InvalidOperationException("Model not trained – call RetrainModelAsync first.");

        // 3) Build PredictionEngine
        var engine = _mlContext.Model
            .CreatePredictionEngine<RatingInput, RatingPrediction>(_model);

        // 4) Load seen and candidate movies
        var seenIds = new HashSet<string>(
            await _db.Ratings
                     .Where(r => r.UserName == username)
                     .Select(r => r.MovieId)
                     .ToListAsync()
        );
        var candidates = await _db.Movies
            .Where(m => !seenIds.Contains(m.MovieId))
            .ToListAsync();

        // 5) Score and filter
        var scored = candidates
            .Select(m =>
            {
                var pred = engine.Predict(new RatingInput {
                    UserName = username,
                    MovieId  = m.MovieId
                });
                return (Movie: m, Score: pred.PredictedScore);
            })
            .Where(x => !float.IsNaN(x.Score) && !float.IsInfinity(x.Score))
            .OrderByDescending(x => x.Score)
            .Take(count)
            .ToArray();

        _logger.LogInformation(
            "Returning top {Count} recommendations for {User}", scored.Length, username);
        return scored;
    }

    public Task<bool> HasRatingsAsync(string username)
        => _db.Ratings.AnyAsync(r => r.UserName == username);

    public async Task EnsureUserHasRatingsAsync(string username)
    {
        if (!await HasRatingsAsync(username))
        {
            _logger.LogWarning("User {User} has no ratings", username);
            throw new InvalidOperationException(
                $"User '{username}' has no ratings. Please scrape first.");
        }
    }
}