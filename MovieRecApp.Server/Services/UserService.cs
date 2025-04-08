using Microsoft.AspNetCore.Identity;
using MovieRecApp.Server.DTOs;
using MovieRecApp.Server.Interfaces;

namespace MovieRecApp.Server.Services;

public class UserService : IUserService
{
    private readonly UserManager<IdentityUser> _userManager;

    public UserService(UserManager<IdentityUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<UserDto?> GetUserProfileAsync(string requestedUsername, string? loggedInUsername)
    {
        var user = await _userManager.FindByNameAsync(requestedUsername);
        if (user == null)
            return null;

        return new UserDto
        {
            Id = user.Id,
            UserName = user.UserName ?? "",
            Email = user.Email ?? "",
            IsCurrentUser = user.UserName == loggedInUsername
        };
    }
}
