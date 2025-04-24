using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using MovieRecApp.Server.Interfaces;

namespace MovieRecApp.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScrapeController : ControllerBase
{
    private readonly ILetterboxdScraper _scraper;
    private readonly ILogger<ScrapeController> _logger;

    public ScrapeController(
        ILetterboxdScraper scraper,
        ILogger<ScrapeController> logger)
    {
        _scraper = scraper;
        _logger  = logger;
    }

    // POST: /api/scrape/{username}
    [HttpPost("{username}")]
    public async Task<IActionResult> Scrape(
        string username,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("ScrapeRatingsForUserAsync called for '{User}'", username);

        try
        {
            await _scraper.ScrapeRatingsForUserAsync(username);
            _logger.LogInformation("ScrapeRatingsForUserAsync succeeded for '{User}'", username);
            return Ok(new { message = "Scraping complete." });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ScrapeRatingsForUserAsync canceled for '{User}'", username);
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scraping user '{User}'", username);
            return StatusCode(500, new { error = "Failed to scrape ratings for user." });
        }
    }
}