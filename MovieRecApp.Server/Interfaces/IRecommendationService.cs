using MovieRecApp.Server.Models;

namespace MovieRecApp.Server.Interfaces;

public interface IRecommendationService
{
    Task RetrainModelAsync();
    Task<RatingPrediction[]> GetTopRecommendationsAsync(
        string username,
        int count
    );
    Task<(Movie Movie, float Score)[]> GetTopRecommendationsWithMoviesAsync(
        string username, int count);
}