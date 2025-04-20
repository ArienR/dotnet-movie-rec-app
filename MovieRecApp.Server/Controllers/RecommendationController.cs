using Microsoft.AspNetCore.Mvc;
using MovieRecApp.Server.Data;
using MovieRecApp.Server.Interfaces;

namespace MovieRecApp.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecommendationController : ControllerBase
{
    private readonly IRecommendationService _recService;

    public RecommendationController(IRecommendationService recService)
        => _recService = recService;

    [HttpPost("retrain")]
    public async Task<IActionResult> Retrain()
    {
        await _recService.RetrainModelAsync();
        return Ok(new { message = "Model retrained." });
    }

    [HttpGet("{username}")]
    public async Task<IActionResult> Get(
        string username,
        [FromQuery] int count = 30)
    {
        // 1) Get raw predictions + movie entities
        var raw = await _recService.GetTopRecommendationsWithMoviesAsync(username, count);

        // 2) Map into DTOs
        var dtos = raw.Select(x => new RecommendationDto
        {
            MovieId        = x.Movie.MovieId,
            Title          = x.Movie.Title,
            PosterUrl      = x.Movie.PosterUrl,
            PredictedScore = x.Score
        }).ToList();

        return Ok(dtos);
    }
}