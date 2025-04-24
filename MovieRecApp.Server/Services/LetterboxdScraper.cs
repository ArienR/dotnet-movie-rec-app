using System.Text.Json;
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

    public LetterboxdScraper(HttpClient http, ApplicationDbContext db)
    {
        _http = http;
        _db = db;
    }

    public async Task ScrapeRatingsForUserAsync(string username)
    {
        // Determine number of pages for this user
        var urlBase = $"https://letterboxd.com/{username}/films/by/date/";
        var doc0 = new HtmlDocument();
        var page0 = await _http.GetStringAsync(urlBase);
        doc0.LoadHtml(page0);

        var pageNodes = doc0.DocumentNode
            .SelectNodes("//li[contains(@class,'paginate-page')]/a");
        int pageCount = pageNodes != null
            ? int.Parse(pageNodes.Last().InnerText.Trim().Replace(",", ""))
            : 1;

        // Loop over each page
        int processed = 0;
        for (int p = 1; p <= pageCount; p++)
        {
            var pageHtml = await _http.GetStringAsync(urlBase + $"page/{p}/");
            var doc = new HtmlDocument();
            doc.LoadHtml(pageHtml);

            var items = doc.DocumentNode
                .SelectNodes("//li[contains(@class,'poster-container')]");
            if (items == null) continue;

            foreach (var item in items)
            {
                // a) pull out the movieId
                var posterDiv = item
                    .SelectSingleNode(".//div[contains(@class,'film-poster')]");
                var link = posterDiv?
                    .GetAttributeValue("data-target-link", "")
                    .Split('/', StringSplitOptions.RemoveEmptyEntries)
                    .Last();
                if (string.IsNullOrEmpty(link)) continue;
                var movieId = link;

                // parse the star-rating
                var ratingCls = item
                    .SelectSingleNode(".//span[contains(@class,'rating')]")
                    ?.GetAttributeValue("class", "") ?? "";
                var score = float.TryParse(ratingCls.Split('-').Last(), out var r)
                    ? r
                    : 0f;

                // upsert skeleton Movie
                var movie = await _db.Movies.FindAsync(movieId);
                if (movie == null)
                {
                    movie = new Movie
                    {
                        MovieId = movieId,
                        Title = movieId
                    };
                    _db.Movies.Add(movie);
                }

                // enrich metadata + poster
                await EnrichFromLetterboxdJsonAsync(movie);
                await EnrichPosterFromAjaxAsync(movie);

                // upsert Rating
                var existing = await _db.Ratings.FindAsync(username, movieId);
                if (existing != null)
                    existing.Score = score;
                else
                    _db.Ratings.Add(new Rating
                    {
                        UserName = username,
                        MovieId = movieId,
                        Score = score
                    });

                // batch-save every 100 movies
                processed++;
                if (processed % 100 == 0)
                    await _db.SaveChangesAsync();
            }
        }

        // final flush
        await _db.SaveChangesAsync();

    }
    
    /// <summary>
    /// Pulls title, year, runtime from Letterboxd's /json/ endpoint.
    /// </summary>
    private async Task EnrichFromLetterboxdJsonAsync(Movie movie)
    {
        var jsonUrl = $"https://letterboxd.com/film/{movie.MovieId}/json/";
        using var res = await _http.GetAsync(jsonUrl);
        if (!res.IsSuccessStatusCode) return;

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var root       = doc.RootElement;

        if (root.TryGetProperty("name", out var nm)) 
            movie.Title   = nm.GetString()!;

        if (root.TryGetProperty("releaseYear", out var yr))  
            movie.Year    = yr.GetInt32();

        if (root.TryGetProperty("runTime", out var rt))      
            movie.Runtime = rt.GetInt32();
    }
    
    /// <summary>
    /// GET /ajax/poster/film/{id}/hero/230x345 then
    /// select 'div.film-poster img', grab its 'src' then
    /// split on '?' (drop query), replace resized-prefix, split at ".jpg", drop if empty-poster
    /// </summary>
    private async Task EnrichPosterFromAjaxAsync(Movie movie)
    {
        // Call the AJAX endpoint
        var ajaxUrl = $"https://letterboxd.com/ajax/poster/film/{movie.MovieId}/hero/230x345";
        var html    = await _http.GetStringAsync(ajaxUrl);

        // Parse with HtmlAgilityPack
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(html);

        // Grab the <img> inside <div class="film-poster">
        var imgNode = doc.DocumentNode
            .SelectSingleNode("//div[contains(@class,'film-poster')]//img");
        if (imgNode == null)
        {
            // nothing found → clear or keep old
            movie.PosterUrl ??= "";
            return;
        }

        // Strip off any query-string (like "?…")
        var srcNoQuery = imgNode
            .GetAttributeValue("src", "")
            .Split('?', 2)[0];

        // Ensure we have the full CDN prefix
        var fullUrl = srcNoQuery;
        if (!fullUrl.StartsWith("https://a.ltrbxd.com/resized/"))
        {
            fullUrl = "https://a.ltrbxd.com/resized/" +
                      (fullUrl.StartsWith("/") ? fullUrl[1..] : fullUrl);
        }

        // Make sure it ends in .jpg
        if (!fullUrl.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
        {
            fullUrl += ".jpg";
        }

        // Ignore the empty-poster placeholder
        if (fullUrl.Contains("https://s.ltrbxd.com/static/img/empty-poster"))
        {
            movie.PosterUrl = "";
        }
        else
        {
            movie.PosterUrl = fullUrl;
        }
    }
}