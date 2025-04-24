using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MovieRecApp.IntegrationTests.TestHelpers;
using MovieRecApp.Server.Data;
using MovieRecApp.Server.Models;
using MovieRecApp.Server.Services;
using NSubstitute;

namespace MovieRecApp.IntegrationTests.Tests;

public class LetterboxdScraperTests
{
    private LetterboxdScraper CreateScraper(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
    {
        var handler = new TestHttpMessageHandler(responder);
        var client  = new HttpClient(handler) { BaseAddress = new Uri("https://letterboxd.com/") };

        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationDbContext(opts);
        var logger = Substitute.For<ILogger<LetterboxdScraper>>();

        return new LetterboxdScraper(client, db, logger);
    }

    [Fact]
    public async Task EnrichFromLetterboxdJsonAsync_ParsesNumbersAndIgnoresNulls()
    {
        var json = @"{""name"":""Test Movie"",""releaseYear"":2022,""runTime"":null}";
        var scraper = CreateScraper(_ => Task.FromResult(new HttpResponseMessage {
            StatusCode = HttpStatusCode.OK,
            Content    = new StringContent(json)
        }));

        var movie = new Movie { MovieId = "test-id" };
        await scraper.EnrichFromLetterboxdJsonAsync(movie);

        Assert.Equal("Test Movie", movie.Title);
        Assert.Equal(2022, movie.Year);
        Assert.Equal(0, movie.Runtime);
    }

    [Fact]
    public async Task EnrichPosterFromAjaxAsync_ExtractsCleanJpgUrl()
    {
        var html = @"<div class=""film-poster"">
                        <img src=""https://a.ltrbxd.com/resized/film-poster/1/2/3-crop.jpg?foo=bar"" />
                     </div>";
        var scraper = CreateScraper(_ => Task.FromResult(new HttpResponseMessage {
            StatusCode = HttpStatusCode.OK,
            Content    = new StringContent(html)
        }));

        var movie = new Movie { MovieId = "test-id" };
        await scraper.EnrichPosterFromAjaxAsync(movie);

        Assert.Equal(
            "https://a.ltrbxd.com/resized/film-poster/1/2/3-crop.jpg",
            movie.PosterUrl
        );
    }
}
