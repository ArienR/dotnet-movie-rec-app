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
    private readonly object _lock = new();

    public RecommendationService(ApplicationDbContext db)
    {
        _db = db;
        _mlContext = new MLContext();

        // Attempt to load existing model
        if (File.Exists(MODEL_PATH))
        {
            try
            {
                using var stream = File.OpenRead(MODEL_PATH);
                _model = _mlContext.Model.Load(stream, out _);
            }
            catch (Exception e)
            {
                // Log and ignore a bad model file so retrain can overwrite it
                Console.Error.WriteLine($"[RecommendationService] Failed to load model: {e.Message}");
                _model = null;
            }
        }
    }

    public async Task RetrainModelAsync()
    {
        // 1) Load all ratings from DB
        var allRatings = await _db.Ratings
            .Select(r => new RatingInput
            {
                UserName = r.UserName,
                MovieId  = r.MovieId,
                Score    = r.Score
            })
            .ToListAsync();

        var dataView = _mlContext.Data.LoadFromEnumerable(allRatings);

        // 2) Build the CF pipeline
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
                    NumberOfIterations          = 20,
                    ApproximationRank           = 100
                }));

        // 3) Train
        var model = pipeline.Fit(dataView);

        // 4) Save to disk
        lock (_lock)
        {
            using var fs = File.OpenWrite(MODEL_PATH);
            _mlContext.Model.Save(model, dataView.Schema, fs);
            _model = model;
        }
    }
    
    public async Task<RatingPrediction[]> GetTopRecommendationsAsync(
        string username,
        int count
    )
    {
        if (_model == null)
            throw new InvalidOperationException("Model not trained. Call RetrainModelAsync first.");

        // 1) Get the list of movies the user already rated
        var seen = await _db.Ratings
            .Where(r => r.UserName == username)
            .Select(r => r.MovieId)
            .ToListAsync();

        // 2) Load *all* movies into memory and filter out 'seen' ones
        var allMovies = await _db.Movies.ToListAsync();
        var candidates = allMovies
            .Where(m => !seen.Contains(m.MovieId));    // IEnumerable.Where → no ambiguity

        // 3) Create a single PredictionEngine (stateful, not thread‑safe, so you might want to use CreatePredictionEnginePool for real apps)
        var engine = _mlContext.Model
            .CreatePredictionEngine<RatingInput, RatingPrediction>(_model);

        // 4) Score and take top N
        var scored = candidates
            .Select(m => (m, score: engine.Predict(
                new RatingInput { UserName = username, MovieId = m.MovieId }
            ).PredictedScore))
            .OrderByDescending(x => x.score)
            .Take(count)
            .ToArray();

        // 5) Return just the predictions (we’ll enrich in the controller)
        return scored
            .Select(x => new RatingPrediction { PredictedScore = x.score })
            .ToArray();
    }
}