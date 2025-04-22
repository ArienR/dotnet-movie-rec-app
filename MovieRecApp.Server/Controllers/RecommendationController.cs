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
    {
        _recService = recService;
    }

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
        try
        {
            // Push the guard into the service
            await _recService.EnsureUserHasRatingsAsync(username);

            var recs = await _recService.GetTopRecommendationsWithMoviesAsync(username, count);
            return Ok(recs.Select(x => new RecommendationDto
            {
                MovieId = x.Movie.MovieId,
                Title = x.Movie.Title,
                PosterUrl = x.Movie.PosterUrl,
                PredictedScore = x.Score
            }));
        }
        catch (InvalidOperationException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }
}