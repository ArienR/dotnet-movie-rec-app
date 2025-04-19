using Microsoft.AspNetCore.Mvc;
using MovieRecApp.Server.Interfaces;

namespace MovieRecApp.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScrapeController : ControllerBase
{
    private readonly ILetterboxdScraper _scraper;

    public ScrapeController(ILetterboxdScraper scraper)
    {
        _scraper = scraper;
    }

    // POST: /api/scrape/{username}
    [HttpPost("{username}")]
    public async Task<IActionResult> Scrape(string username)
    {
        // TODO: validate username format
        await _scraper.ScrapeRatingsForUserAsync(username);
        return Ok(new { message = "Scraping complete" });
    }
}