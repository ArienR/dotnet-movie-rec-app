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

public class LetterboxdScraper : ILetterboxdScraper
{
    private readonly HttpClient _http;
    private readonly ApplicationDbContext _db;
    // limit concurrent HTTP requests to avoid overwhelming Letterboxd
    private static readonly SemaphoreSlim _httpSemaphore = new(4);

    public LetterboxdScraper(HttpClient http, ApplicationDbContext db)
    {
        _http = http;
        _db = db;
    }

    public async Task ScrapeRatingsForUserAsync(string username)
    {
        // 1) Collect all (movieId, score) pairs by scraping the user's pages
        var ratingsList = new List<(string movieId, float score)>();
        var urlBase = $"https://letterboxd.com/{username}/films/by/date/";

        var doc0 = new HtmlDocument();
        var firstPage = await _http.GetStringAsync(urlBase);
        doc0.LoadHtml(firstPage);
        var pageNodes = doc0.DocumentNode.SelectNodes("//li[contains(@class,'paginate-page')]/a");
        int pageCount = pageNodes != null
            ? int.Parse(pageNodes.Last().InnerText.Trim().Replace(",", ""))
            : 1;

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

                ratingsList.Add((movieId, score));
            }
        }

        if (!ratingsList.Any())
            return;

        // 2) Preload existing Movies and Ratings in a single batch each
        var movieIds = ratingsList.Select(r => r.movieId).Distinct().ToList();
        var existingMovies = await _db.Movies
            .AsNoTracking()
            .Where(m => movieIds.Contains(m.MovieId))
            .ToDictionaryAsync(m => m.MovieId);

        var existingRatings = await _db.Ratings
            .AsNoTracking()
            .Where(r => r.UserName == username && movieIds.Contains(r.MovieId))
            .ToDictionaryAsync(r => r.MovieId);

        // 3) Disable change-tracking for speed
        _db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            // 4) Process each rating: upsert Movie, enrich JSON/poster if needed, upsert Rating
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

            // 5) Await all enrichment (with limited concurrency)
            await Task.WhenAll(enrichTasks);

            // 6) Final save
            await _db.SaveChangesAsync();
        }
        finally
        {
            _db.ChangeTracker.AutoDetectChangesEnabled = true;
        }
    }

    private async Task EnrichFromLetterboxdJsonAsync(Movie movie)
    {
        await _httpSemaphore.WaitAsync();
        try
        {
            var jsonUrl = $"https://letterboxd.com/film/{movie.MovieId}/json/";
            using var res = await _http.GetAsync(jsonUrl);
            if (!res.IsSuccessStatusCode) return;

            using var doc  = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            var     root = doc.RootElement;

            if (root.TryGetProperty("name", out var nm)
                && nm.ValueKind == JsonValueKind.String)
            {
                movie.Title = nm.GetString();
            }

            if (root.TryGetProperty("releaseYear", out var yr)
                && yr.ValueKind == JsonValueKind.Number)
            {
                movie.Year = yr.GetInt32();
            }

            if (root.TryGetProperty("runTime", out var rt)
                && rt.ValueKind == JsonValueKind.Number)
            {
                movie.Runtime = rt.GetInt32();
            }
        }
        finally
        {
            _httpSemaphore.Release();
        }
    }

    private async Task EnrichPosterFromAjaxAsync(Movie movie)
    {
        await _httpSemaphore.WaitAsync();
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
            _httpSemaphore.Release();
        }
    }
}
