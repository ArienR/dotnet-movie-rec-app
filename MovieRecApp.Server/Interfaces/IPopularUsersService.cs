using System.Collections.Generic;
using System.Threading.Tasks;

namespace MovieRecApp.Server.Interfaces;

/// <summary>
/// Fetches the top popular Letterboxd usernames and seeds their ratings.
/// </summary>
public interface IPopularUsersService
{
    /// <summary>
    /// Returns the list of usernames from the popular-this-month pages.
    /// </summary>
    Task<List<string>> GetPopularUsernamesAsync(int pages);

    /// <summary>
    /// Scrapes and saves ratings for each popular user.
    /// </summary>
    Task SeedPopularUsersAsync(int pages);
}