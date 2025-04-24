using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MovieRecApp.IntegrationTests.TestHelpers;
using MovieRecApp.Server.Data;
using MovieRecApp.Server.Interfaces;
using MovieRecApp.Server.Services;
using NSubstitute;

namespace MovieRecApp.IntegrationTests.Tests;

public class PopularUsersServiceTests
{
    private PopularUsersService CreateService(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
    {
        var handler = new TestHttpMessageHandler(responder);
        var client  = new HttpClient(handler) { BaseAddress = new Uri("https://letterboxd.com/") };

        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationDbContext(opts);

        // ← NSubstitute for the scraper interface
        var scraper = Substitute.For<ILetterboxdScraper>();
        var logger = Substitute.For<ILogger<PopularUsersService>>();

        return new PopularUsersService(client, scraper, db, logger);
    }

    [Fact]
    public async Task GetPopularUsernamesAsync_CombinesAndDedupsAcrossPages()
    {
        var page1 = @"<table class=""person-table"">
                        <td class=""table-person""><a href=""/user1/""></a></td>
                        <td class=""table-person""><a href=""/user2/""></a></td>
                      </table>";
        var page2 = @"<table class=""person-table"">
                        <td class=""table-person""><a href=""/user2/""></a></td>
                        <td class=""table-person""><a href=""/user3/""></a></td>
                      </table>";

        var seq = new Queue<string>(new[] { page1, page2 });
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responder = _ =>
            Task.FromResult(new HttpResponseMessage {
                StatusCode = HttpStatusCode.OK,
                Content    = new StringContent(seq.Dequeue())
            });

        var svc = CreateService(responder);
        var result = await svc.GetPopularUsernamesAsync(2);

        Assert.Equal(3, result.Count);
        Assert.Contains("user1", result);
        Assert.Contains("user2", result);
        Assert.Contains("user3", result);
    }
}
