// MovieRecApp.Server.Services/TmdbService.cs

using System.ComponentModel.DataAnnotations;
using MovieRecApp.Server.Interfaces;
using MovieRecApp.Server.Models;

namespace MovieRecApp.Server.Services;

public class TmdbService : ITmdbService
{
    private readonly HttpClient _http;
    [Required]
    private readonly string _apiKey;

    public TmdbService(HttpClient http, IConfiguration config)
    {
        _http   = http;
        _apiKey = config["Tmdb:ApiKey"];
    }

    public async Task EnrichMovieAsync(Movie movie)
    {
        // call /search/movie
        var url    = $"search/movie?api_key={_apiKey}&query={Uri.EscapeDataString(movie.Title)}";
        var result = await _http.GetFromJsonAsync<SearchResult>(url);
        var first  = result?.Results?.FirstOrDefault();
        if (first == null) return;

        // enrich entity
        movie.TmdbId    = first.Id.ToString();
        movie.PosterUrl = string.IsNullOrEmpty(first.PosterPath)
            ? movie.PosterUrl
            : $"https://image.tmdb.org/t/p/w500{first.PosterPath}";
        movie.Title     = first.Title; // or: $"{first.Title} ({first.ReleaseDate?.Year})"
    }

    // helper DTOs
    private class SearchResult
    {
        public TmdbMovie[] Results { get; set; }
    }

    private class TmdbMovie
    {
        public int      Id          { get; set; }
        public string   Title       { get; set; }
        public string   PosterPath  { get; set; }
        public DateTime? ReleaseDate{ get; set; }
    }
}