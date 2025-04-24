using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using MovieRecApp.Server.Data;
using MovieRecApp.Server.Interfaces;
using MovieRecApp.Server.Models;


namespace MovieRecApp.Server.Services;

public class LetterboxdScraper : ILetterboxdScraper
{
    private readonly HttpClient _http;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<LetterboxdScraper> _logger;
    private static readonly SemaphoreSlim HttpSemaphore = new(4);

    public LetterboxdScraper(HttpClient http, ApplicationDbContext db, ILogger<LetterboxdScraper> logger)
    {
        _http = http;
        _db = db;
        _logger = logger;
    }

    public async Task ScrapeRatingsForUserAsync(string username)
    {
        _logger.LogInformation("Starting scrape for user {User}", username);
        // Collect all (movieId, score) pairs by scraping the user's pages before adding it all to the db
        var ratingsList = new List<(string movieId, float score)>();
        var urlBase = $"https://letterboxd.com/{username}/films/by/date/";

        var doc0 = new HtmlDocument();
        var firstPage = await _http.GetStringAsync(urlBase);
        doc0.LoadHtml(firstPage);
        var pageNodes = doc0.DocumentNode.SelectNodes("//li[contains(@class,'paginate-page')]/a");
        int pageCount = pageNodes != null
            ? int.Parse(pageNodes.Last().InnerText.Trim().Replace(",", ""))
            : 1;
        _logger.LogInformation("User {User} has {Pages} pages to scrape", username, pageCount);
        
        for (int page = 1; page <= pageCount; page++)
        {
            var html = page == 1
                ? firstPage
                : await _http.GetStringAsync(urlBase + $"page/{page}/");

            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var items = doc.DocumentNode.SelectNodes("//li[contains(@class,'poster-container')]");
            if (items == null) continue;

            foreach (var item in items)
            {
                var posterDiv = item.SelectSingleNode(".//div[contains(@class,'film-poster')]");
                var link = posterDiv?.GetAttributeValue("data-target-link", "");
                var movieId = link?.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).Last();
                if (string.IsNullOrEmpty(movieId)) continue;

                var ratingCls = item.SelectSingleNode(".//span[contains(@class,'rating')]")
                                 ?.GetAttributeValue("class", "");
                var score = float.TryParse(ratingCls?.Split('-').Last(), out var r)
                    ? r
                    : 0f;

                if (score > 0)
                {
                    ratingsList.Add((movieId, score));
                }
            }
        }

        if (!ratingsList.Any())
            return;

        var movieIds = ratingsList.Select(r => r.movieId).Distinct().ToList();
        var existingMovies = await _db.Movies
            .AsNoTracking()
            .Where(m => movieIds.Contains(m.MovieId))
            .ToDictionaryAsync(m => m.MovieId);

        var existingRatings = await _db.Ratings
            .AsNoTracking()
            .Where(r => r.UserName == username && movieIds.Contains(r.MovieId))
            .ToDictionaryAsync(r => r.MovieId);

        _db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            // Process each rating: upsert Movie, enrich JSON/poster if needed, upsert Rating
            var enrichTasks = new List<Task>();
            foreach (var (movieId, score) in ratingsList)
            {
                // upsert Movie skeleton
                _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                Movie movie;
                if (!existingMovies.TryGetValue(movieId, out movie))
                {
                    movie = new Movie { MovieId = movieId, Title = movieId };
                    _db.Movies.Add(movie);
                }

                // conditional enrich JSON
                if (string.IsNullOrEmpty(movie.Title) || movie.Year == 0)
                {
                    enrichTasks.Add(Task.Run(async () =>
                    {
                        await EnrichFromLetterboxdJsonAsync(movie);
                    }));
                }

                // conditional enrich Poster
                if (string.IsNullOrEmpty(movie.PosterUrl))
                {
                    enrichTasks.Add(Task.Run(async () =>
                    {
                        await EnrichPosterFromAjaxAsync(movie);
                    }));
                }

                // upsert Rating
                if (existingRatings.TryGetValue(movieId, out var existing))
                {
                    existing.Score = score;
                    _db.Ratings.Update(existing);
                }
                else
                {
                    _db.Ratings.Add(new Rating { UserName = username, MovieId = movieId, Score = score });
                }
            }

            // Await all enrichment (with limited concurrency)
            await Task.WhenAll(enrichTasks);

            await _db.SaveChangesAsync();
        }
        finally
        {
            _db.ChangeTracker.AutoDetectChangesEnabled = true;
        }
    }

    internal async Task EnrichFromLetterboxdJsonAsync(Movie movie)
    {
        _logger.LogDebug("Fetching JSON for movie {MovieId}", movie.MovieId);
        await HttpSemaphore.WaitAsync();
        try
        {
            var jsonUrl = $"https://letterboxd.com/film/{movie.MovieId}/json/";
            using var res = await _http.GetAsync(jsonUrl);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("JSON fetch failed for {MovieId}: {Status}", movie.MovieId, res.StatusCode);
                return;
            }

            using var doc  = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            var     root = doc.RootElement;

            if (root.TryGetProperty("name", out var nm)
                && nm.ValueKind == JsonValueKind.String)
            {
                movie.Title = nm.GetString();
            }

            if (root.TryGetProperty("releaseYear", out var yr)
                && yr.ValueKind == JsonValueKind.Number)    // ← guard here
            {
                movie.Year = yr.GetInt32();
            }

            if (root.TryGetProperty("runTime", out var rt)
                && rt.ValueKind == JsonValueKind.Number)    // ← and here
            {
                movie.Runtime = rt.GetInt32();
            }
        }
        finally
        {
            HttpSemaphore.Release();
        }
    }


    internal async Task EnrichPosterFromAjaxAsync(Movie movie)
    {
        _logger.LogDebug("Fetching poster for movie {MovieId}", movie.MovieId);
        await HttpSemaphore.WaitAsync();
        try
        {
            var ajaxUrl = $"https://letterboxd.com/ajax/poster/film/{movie.MovieId}/hero/230x345";
            var html = await _http.GetStringAsync(ajaxUrl);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var imgNode = doc.DocumentNode
                .SelectSingleNode("//div[contains(@class,'film-poster')]//img");
            if (imgNode == null)
            {
                _logger.LogWarning("No poster <img> found for {MovieId}", movie.MovieId);
                movie.PosterUrl = string.Empty;
                return;
            }
            var srcNoQuery = imgNode.GetAttributeValue("src", string.Empty)
                                    .Split('?', 2)[0];
            var fullUrl = srcNoQuery.StartsWith("https://a.ltrbxd.com/resized/")
                ? srcNoQuery
                : "https://a.ltrbxd.com/resized/" + srcNoQuery.TrimStart('/');
            if (!fullUrl.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                fullUrl += ".jpg";

            if (fullUrl.Contains("https://s.ltrbxd.com/static/img/empty-poster"))
                movie.PosterUrl = string.Empty;
            else
                movie.PosterUrl = fullUrl;
        }
        finally
        {
            HttpSemaphore.Release();
        }
    }
}