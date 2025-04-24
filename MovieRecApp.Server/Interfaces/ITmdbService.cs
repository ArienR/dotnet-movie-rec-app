using System.Threading.Tasks;
using MovieRecApp.Server.Models;

namespace MovieRecApp.Server.Interfaces;

public interface ITmdbService
{
    /// <summary>
    /// Given a Movie (with Movie.Title), looks up TMDb and populates:
    ///    - TmdbId
    ///    - PosterUrl (full w500 URL)
    ///    - optionally updates Title to the TMDb canonical title
    /// </summary>
    Task EnrichMovieAsync(Movie movie);
}