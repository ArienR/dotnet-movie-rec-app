using Microsoft.AspNetCore.Mvc;
using MovieRecApp.Server.Interfaces;

namespace MovieRecApp.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PopularUsersController : ControllerBase
{
    private readonly IPopularUsersService _svc;

    public PopularUsersController(IPopularUsersService svc)
    {
        _svc = svc;
    }

    // POST /api/popularusers/seed?pages=5
    [HttpPost("seed")]
    public async Task<IActionResult> Seed([FromQuery] int pages = 5)
    {
        // Scrape the top 'pages' pages of popular-this-month users
        await _svc.SeedPopularUsersAsync(pages);
        return Ok(new { message = $"Seeded ratings for popular users (pages={pages})." });
    }
}