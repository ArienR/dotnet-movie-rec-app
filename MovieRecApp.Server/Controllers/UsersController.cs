namespace MovieRecApp.Server.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;

[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager;

    public UsersController(UserManager<IdentityUser> userManager)
    {
        _userManager = userManager;
    }
    
    // GET: /api/users/{username}
    /// <summary>
    /// Retrieves the authenticated user's profile by username.
    /// </summary>
    /// <param name="username">The username of the requested user.</param>
    /// <returns>User profile details if authorized.</returns>
    [Authorize]
    [HttpGet("{username}")]
    public async Task<IActionResult> GetUserProfile(string username)
    {
        // Extract authenticated user's username from JWT token
        var loggedInUsername = User.FindFirstValue(ClaimTypes.Name);

        if (loggedInUsername == null)
        {
            // Username not found within token
            return Unauthorized(new { message = "Invalid token: username not found." });
        }
        
        // Ensure logged-in user is accessing their own info
        if (loggedInUsername != username)
        {
            return Forbid(); // 403 Forbidden
        }
        
        // Retrieve user details from db
        var user = await _userManager.FindByNameAsync(username);
        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }
        
        return Ok(new { user.Id, user.UserName, user.Email });
    }
}