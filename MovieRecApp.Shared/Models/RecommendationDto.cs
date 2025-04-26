namespace MovieRecApp.Server.Data;

public class RecommendationDto
{
    public string MovieId   { get; set; }
    public string Title     { get; set; }
    public string PosterUrl { get; set; }
    public string LetterboxdUrl 
        => $"https://letterboxd.com/film/{MovieId}/";
    public float PredictedScore { get; set; }
}