using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MovieRecApp.Server.Interfaces;

namespace MovieRecApp.Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }
    
    // GET: /api/users/{username}
    /// <summary>
    /// Retrieves the user's profile by username.
    /// </summary>
    /// <param name="username">The username of the requested user.</param>
    /// <returns>User profile details.</returns>
    [HttpGet("{username}")]
    public async Task<IActionResult> GetUserProfile(string username)
    {
        // May be null if not logged in
        var loggedInUsername = User.FindFirstValue(ClaimTypes.Name);

        var userDto = await _userService.GetUserProfileAsync(username, loggedInUsername);
        if (userDto == null)
        {
            return NotFound(new { message = "User not found." });
        }

        return Ok(userDto);
    }
}