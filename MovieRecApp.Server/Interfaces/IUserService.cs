using MovieRecApp.Server.DTOs;

namespace MovieRecApp.Server.Interfaces;

public interface IUserService
{
    Task<UserDto?> GetUserProfileAsync(string requestedUsername, string? loggedInUsername);
}
