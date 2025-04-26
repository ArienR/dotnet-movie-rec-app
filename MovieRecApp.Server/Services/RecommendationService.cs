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
            _logger.LogInformation("Retraining recommendation model with example‐weighting…");

            // 1) Fetch all ratings
            var allRatings = await _db.Ratings
                .Select(r => new RatingInput {
                    UserName = r.UserName,
                    MovieId  = r.MovieId,
                    Score    = r.Score
                })
                .ToListAsync();

            // 2) Only keep movies with ≥5 ratings
            var popularMovieIds = allRatings
                .GroupBy(r => r.MovieId)
                .Where(g => g.Count() >= 5)
                .Select(g => g.Key)
                .ToHashSet();
            var filtered = allRatings
                .Where(r => popularMovieIds.Contains(r.MovieId))
                .ToList();

            // 3) “Weight” extremes by duplicating those records
            var weighted = new List<RatingInput>();
            foreach (var r in filtered)
            {
                weighted.Add(r);
                if (r.Score <= 2 || r.Score >= 9)
                    weighted.Add(r);    // duplicate once for double weight
            }

            // 4) Train MF on the weighted data
            var dataView = _mlContext.Data.LoadFromEnumerable(weighted);
            var pipeline = _mlContext.Transforms
                .Conversion.MapValueToKey("userKey", nameof(RatingInput.UserName))
                .Append(_mlContext.Transforms
                    .Conversion.MapValueToKey("movieKey", nameof(RatingInput.MovieId)))
                .Append(_mlContext.Recommendation()
                    .Trainers.MatrixFactorization(new MatrixFactorizationTrainer.Options
                    {
                        MatrixColumnIndexColumnName = "userKey",
                        MatrixRowIndexColumnName    = "movieKey",
                        LabelColumnName             = "Label",
                        NumberOfIterations          = 30,
                        ApproximationRank           = 150,
                        Lambda                      = 0.1
                    }));

            _model = pipeline.Fit(dataView);

            // 5) Persist
            await using var fs = File.Create(MODEL_PATH);
            _mlContext.Model.Save(_model, dataView.Schema, fs);
            _logger.LogInformation("Weighted model retrained and saved to {Path}", MODEL_PATH);
        }
        finally
        {
            _retrainLock.Release();
        }
    }
    
    public async Task EvaluateHoldoutAsync(float testFraction = 0.2f)
    {
        // 1) Fetch & filter
        var allRatings = await _db.Ratings
            .Select(r => new RatingInput {
                UserName = r.UserName,
                MovieId  = r.MovieId,
                Score    = r.Score
            })
            .ToListAsync();
        var popularMovieIds = allRatings
            .GroupBy(r => r.MovieId)
            .Where(g => g.Count() >= 5)
            .Select(g => g.Key)
            .ToHashSet();
        var filtered = allRatings
            .Where(r => popularMovieIds.Contains(r.MovieId))
            .ToList();

        // 2) Apply the same weighting‐by‐duplication
        var weighted = new List<RatingInput>();
        foreach (var r in filtered)
        {
            weighted.Add(r);
            if (r.Score <= 2 || r.Score >= 9)
                weighted.Add(r);
        }

        // 3) Split train/test
        var dataView = _mlContext.Data.LoadFromEnumerable(weighted);
        var split    = _mlContext.Data.TrainTestSplit(dataView, testFraction: testFraction);
        var trainSet = split.TrainSet;
        var testSet  = split.TestSet;

        // 4) Train & evaluate
        var pipeline = _mlContext.Transforms
            .Conversion.MapValueToKey("userKey", nameof(RatingInput.UserName))
            .Append(_mlContext.Transforms
                .Conversion.MapValueToKey("movieKey", nameof(RatingInput.MovieId)))
            .Append(_mlContext.Recommendation()
                .Trainers.MatrixFactorization(new MatrixFactorizationTrainer.Options
                {
                    MatrixColumnIndexColumnName = "userKey",
                    MatrixRowIndexColumnName    = "movieKey",
                    LabelColumnName             = "Label",
                    NumberOfIterations          = 30,
                    ApproximationRank           = 150,
                    Lambda                      = 0.1
                }));
        var model   = pipeline.Fit(trainSet);
        var preds   = model.Transform(testSet);
        var metrics = _mlContext.Regression.Evaluate(preds, "Label", "Score");

        Console.WriteLine($"=== Hold-out (weighted+filtered) {1-testFraction:P0}/{testFraction:P0} ===");
        Console.WriteLine($"  RMSE = {metrics.RootMeanSquaredError:F3}");
        Console.WriteLine($"  MAE  = {metrics.MeanAbsoluteError:F3}");
        Console.WriteLine($"  R²   = {metrics.RSquared:F3}");
    }

    public async Task<(Movie Movie, float Score)[]> GetTopRecommendationsWithMoviesAsync(
        string username, int count)
    {
        // A) Scrape & retrain if needed
        var before = await _db.Ratings.CountAsync(r => r.UserName == username);
        await _scraper.ScrapeRatingsForUserAsync(username);
        var after  = await _db.Ratings.CountAsync(r => r.UserName == username);
        if (_model == null || after > before)
            await RetrainModelAsync();

        // B) Build popularity counts
        var movieCounts = await _db.Ratings
            .GroupBy(r => r.MovieId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        // C) Get unseen candidates
        var seen = await _db.Ratings
            .Where(r => r.UserName == username)
            .Select(r => r.MovieId)
            .ToHashSetAsync();
        var candidates = await _db.Movies
            .Where(m => !seen.Contains(m.MovieId))
            .ToListAsync();

        // D) Score + pop-boost
        var engine = _mlContext.Model
            .CreatePredictionEngine<RatingInput, RatingPrediction>(_model);
        const float popWeight = 0.05f;
        var scored = candidates
            .Select(m =>
            {
                var baseScore = engine.Predict(new RatingInput {
                    UserName = username,
                    MovieId  = m.MovieId
                }).PredictedScore;

                var cnt = movieCounts.GetValueOrDefault(m.MovieId, 0);
                var boost = popWeight * (float)Math.Log(cnt + 1);
                return (Movie: m, Score: baseScore + boost);
            })
            .Where(x => !float.IsNaN(x.Score))
            .OrderByDescending(x => x.Score)
            .Take(count)
            .ToArray();

        _logger.LogInformation("Returning top {Count} recommendations for {User}", scored.Length, username);
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