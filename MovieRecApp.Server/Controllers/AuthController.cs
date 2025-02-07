using Microsoft.AspNetCore.Identity.Data;

namespace MovieRecApp.Server.Controllers;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Models;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager;

    public AuthController(UserManager<IdentityUser> userManager)
    {
        _userManager = userManager; // On init inject userManager
    }
    
    // POST: /api/auth/register
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest registerRequest)
    {
        // Validate input
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }
        
        // Check if user already exists
        var existingUser = await _userManager.FindByEmailAsync(registerRequest.Email);
        if (existingUser != null)
        {
            return BadRequest(new { message = "Email is already in use." });
        }
        
        // Create a new user
        var user = new IdentityUser
        {
            UserName = registerRequest.UserName,
            Email = registerRequest.Email
        };
        
        var result = await _userManager.CreateAsync(user, registerRequest.Password);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        return Created("", new { message = "User created successfully." });
    }
}