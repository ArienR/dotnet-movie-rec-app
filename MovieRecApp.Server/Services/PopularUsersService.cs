using HtmlAgilityPack;
using MovieRecApp.Server.Data;
using MovieRecApp.Server.Interfaces;

namespace MovieRecApp.Server.Services;

public class PopularUsersService : IPopularUsersService
{
    private readonly HttpClient _http;
    private readonly ILetterboxdScraper _scraper;
    private readonly ApplicationDbContext _db;

    public PopularUsersService(HttpClient http, ILetterboxdScraper scraper, ApplicationDbContext db)
    {
        _http    = http;
        _scraper = scraper;
        _db = db;
    }

    public async Task<List<string>> GetPopularUsernamesAsync(int pages)
    {
        var users = new List<string>();

        for (int p = 1; p <= pages; p++)
        {
            var url  = $"https://letterboxd.com/members/popular/this/week/page/{p}/";
            var html = await _http.GetStringAsync(url);
            var doc  = new HtmlDocument();
            doc.LoadHtml(html);

            // 1) Find the table of members
            var table = doc.DocumentNode.SelectSingleNode(
                "//table[contains(@class,'person-table')]");
            if (table == null) 
                continue;

            // 2) Each <td class="table-person"> is one user
            var rows = table.SelectNodes(".//td[contains(@class,'table-person')]");
            if (rows == null) 
                continue;

            foreach (var row in rows)
            {
                var a = row.SelectSingleNode(".//a");
                var href = a?.GetAttributeValue("href", "");
                if (string.IsNullOrEmpty(href)) 
                    continue;

                var username = href.Trim('/');
                if (!users.Contains(username))
                    users.Add(username);
            }
        }

        return users;
    }


    public async Task SeedPopularUsersAsync(int pages)
    {
        var users = await GetPopularUsernamesAsync(pages);
        Console.WriteLine($"[Seeder] Found {users.Count} users to seed.");
        foreach (var u in users)
        {
            try
            {
                Console.WriteLine($"[Seeder] Scraping {u}...");
                await _scraper.ScrapeRatingsForUserAsync(u);
                Console.WriteLine($"[Seeder] Done {u}. DB now has {_db.Ratings.Count(r => r.UserName == u)} ratings.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Seeder] Failed {u}: {ex.Message}");
            }
        }
    }
}