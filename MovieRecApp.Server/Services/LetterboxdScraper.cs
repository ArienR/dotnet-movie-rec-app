using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using MovieRecApp.Server.Data;
using MovieRecApp.Server.Interfaces;
using MovieRecApp.Server.Models;
using MovieRecApp.Server.Services;

public class LetterboxdScraper : ILetterboxdScraper
{
    private readonly HttpClient _http;
    private readonly ApplicationDbContext _db;
    private readonly ITmdbService _tmdbService;

    public LetterboxdScraper(HttpClient http, ApplicationDbContext db, ITmdbService tmdb)
    {
        _http = http;
        _db = db;
        _tmdbService = tmdb;
    }

    public async Task ScrapeRatingsForUserAsync(string username)
    {
        // 1) Determine number of pages for this user
        var urlBase = $"https://letterboxd.com/{username}/films/by/date/";
        var doc0 = new HtmlDocument();
        var page0 = await _http.GetStringAsync(urlBase);
        doc0.LoadHtml(page0);

        // Look for paginate links
        var pageNodes = doc0.DocumentNode
            .SelectNodes("//li[contains(@class,'paginate-page')]/a");
        int pageCount = pageNodes != null
            ? pageNodes.Last().InnerText.Trim().Replace(",", "") is string t && int.TryParse(t, out var n) ? n : 1
            : 1;

        // 2) Loop over each page
        for (int p = 1; p <= pageCount; p++)
        {
            var pageHtml = await _http.GetStringAsync(urlBase + $"page/{p}/");
            var doc = new HtmlDocument();
            doc.LoadHtml(pageHtml);

            // Each review is in <li class="poster-container">
            var items = doc.DocumentNode.SelectNodes("//li[contains(@class,'poster-container')]");
            if (items == null) continue;

            foreach (var item in items)
            {
                // MovieId (still using the film-poster div)
                var posterDiv = item
                    .SelectSingleNode(".//div[contains(@class,'film-poster')]");
                var link = posterDiv?
                    .GetAttributeValue("data-target-link", "")
                    .Split('/', StringSplitOptions.RemoveEmptyEntries)
                    .Last();
                var movieId = link;
                if (string.IsNullOrEmpty(movieId)) 
                    continue;

                // Star rating (unchanged)
                var ratingCls = item
                    .SelectSingleNode(".//span[contains(@class,'rating')]")
                    ?.GetAttributeValue("class", "") ?? "";
                var score = ratingCls
                    .Split('-').Last() is string s && float.TryParse(s, out var r) 
                    ? r 
                    : 0;

                // Upsert or retrieve the Movie skeleton
                var movie = await _db.Movies.FindAsync(movieId);
                if (movie == null)
                {
                    movie = new Movie
                    {
                        MovieId = movieId,
                        Title   = movieId
                    };
                    _db.Movies.Add(movie);
                }
                
                await _tmdbService.EnrichMovieAsync(movie);

                // Upsert Rating
                var existing = await _db.Ratings.FindAsync(username, movieId);
                if (existing != null)
                {
                    existing.Score = score;
                }
                else
                {
                    _db.Ratings.Add(new Rating
                    {
                        UserName = username,
                        MovieId  = movieId,
                        Score    = score
                    });
                }
            }

            // Save per page to avoid huge batches
            await _db.SaveChangesAsync();
        }
    }
}