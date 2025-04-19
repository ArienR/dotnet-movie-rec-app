using Microsoft.ML.Data;

namespace MovieRecApp.Server.Models;

public class RatingPrediction
{
    [ColumnName("Score")]
    public float PredictedScore { get; set; }
}