using Microsoft.AspNetCore.Mvc;
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
        var preds = await _recService.GetTopRecommendationsAsync(username, count);
        return Ok(preds);
    }
}