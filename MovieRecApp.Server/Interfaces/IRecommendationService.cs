﻿using MovieRecApp.Server.Models;

namespace MovieRecApp.Server.Interfaces;

public interface IRecommendationService
{
    Task RetrainModelAsync();
    Task<(Movie Movie, float Score)[]> GetTopRecommendationsWithMoviesAsync(
        string username, int count);
    Task<bool> HasRatingsAsync(string username);
    Task EnsureUserHasRatingsAsync(string username);
}