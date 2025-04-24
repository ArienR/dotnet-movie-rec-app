using HtmlAgilityPack;
using MovieRecApp.Server.Data;
using MovieRecApp.Server.Interfaces;

namespace MovieRecApp.Server.Services;

public class PopularUsersService : IPopularUsersService
{
    private readonly HttpClient _http;
    private readonly ILetterboxdScraper _scraper;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<PopularUsersService> _logger;

    public PopularUsersService(
        HttpClient http,
        ILetterboxdScraper scraper,
        ApplicationDbContext db,
        ILogger<PopularUsersService> logger)
    {
        _http    = http;
        _scraper = scraper;
        _db      = db;
        _logger  = logger;
    }

    public async Task<List<string>> GetPopularUsernamesAsync(int pages)
    {
        var users = new HashSet<string>();

        for (int p = 1; p <= pages; p++)
        {
            var url = $"https://letterboxd.com/members/popular/this/week/page/{p}/";
            _logger.LogInformation("Fetching popular users page {Page}", p);

            HttpResponseMessage response;
            try
            {
                response = await _http.GetAsync(url);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch {Url}", url);
                continue;
            }
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Non-success status {StatusCode} for {Url}", response.StatusCode, url);
                continue;
            }

            var html = await response.Content.ReadAsStringAsync();
            var doc  = new HtmlDocument();
            doc.LoadHtml(html);

            var table = doc.DocumentNode
                .SelectSingleNode("//table[contains(@class,'person-table')]");
            if (table == null) continue;

            var rows = table.SelectNodes(".//td[contains(@class,'table-person')]");
            if (rows == null) continue;

            foreach (var row in rows)
            {
                var a    = row.SelectSingleNode(".//a");
                var href = a?.GetAttributeValue("href", "");
                if (string.IsNullOrEmpty(href)) continue;

                var username = href.Trim('/');
                if (users.Add(username))
                    _logger.LogDebug("Added user {User}", username);
                else
                    _logger.LogDebug("Duplicate user {User} skipped", username);
            }
        }

        _logger.LogInformation("Total unique users found: {Count}", users.Count);
        return users.ToList();
    }

    public async Task SeedPopularUsersAsync(int pages)
    {
        var users = await GetPopularUsernamesAsync(pages);
        _logger.LogInformation("Found {Count} popular users to seed", users.Count);

        foreach (var u in users)
        {
            try
            {
                _logger.LogInformation("Scraping ratings for user {User}", u);
                await _scraper.ScrapeRatingsForUserAsync(u);
                var count = _db.Ratings.Count(r => r.UserName == u);
                _logger.LogInformation("Done {User}: {Count} ratings", u, count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed seeding user {User}", u);
            }
        }
    }
}