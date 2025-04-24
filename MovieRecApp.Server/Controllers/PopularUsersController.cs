using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using MovieRecApp.Server.Interfaces;

namespace MovieRecApp.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PopularUsersController : ControllerBase
{
    private readonly IPopularUsersService _svc;
    private readonly ILogger<PopularUsersController> _logger;

    public PopularUsersController(
        IPopularUsersService svc,
        ILogger<PopularUsersController> logger)
    {
        _svc    = svc;
        _logger = logger;
    }

    [HttpPost("seed")]
    public async Task<IActionResult> Seed(
        [FromQuery, Range(1, 20)] int pages = 5,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SeedPopularUsers called with pages={Pages}", pages);
        try
        {
            await _svc.SeedPopularUsersAsync(pages);
            _logger.LogInformation("SeedPopularUsers completed for pages={Pages}", pages);
            return Ok(new { message = $"Seeded ratings for popular users (pages={pages})." });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("SeedPopularUsers canceled by client");
            return StatusCode(499);
        }
        catch (ValidationException vex)
        {
            _logger.LogWarning(vex, "Invalid pages parameter: {Pages}", pages);
            return BadRequest(new { error = vex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SeedPopularUsers for pages={Pages}", pages);
            return StatusCode(500, new { error = "An unexpected error occurred." });
        }
    }
}