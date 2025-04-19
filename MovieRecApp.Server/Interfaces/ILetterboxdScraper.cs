namespace MovieRecApp.Server.Interfaces;

/// <summary>
/// Scrapes Letterboxd profiles for movie ratings.
/// </summary>
public interface ILetterboxdScraper
{
    /// <summary>
    /// Scrape the given Letterboxd username's ratings and persist them.
    /// </summary>
    Task ScrapeRatingsForUserAsync(string username);
}