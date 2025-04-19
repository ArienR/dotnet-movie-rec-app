using Microsoft.ML.Data;

namespace MovieRecApp.Server.Models;

public class RatingInput
{
    [LoadColumn(0)]
    public string UserName { get; set; }

    [LoadColumn(1)]
    public string MovieId { get; set; }

    [LoadColumn(2), ColumnName("Label")]
    public float Score { get; set; }
}