using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MovieRecApp.Server.Interfaces;
using MovieRecApp.Shared.Models;

namespace MovieRecApp.Server.Controllers;

[Route("api/auth")]
[ApiController]
[EnableCors("AllowBlazorClient")]
public class AuthController : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly IJwtService _jwtService;

    public AuthController(UserManager<IdentityUser> userManager, IConfiguration configuration, IJwtService jwtService)
    {
        _userManager = userManager; // On init inject userManager
        _configuration = configuration; // On init inject appsettings.json
        _jwtService = jwtService;
    }
    
    // POST: /api/auth/register
    /// <summary>
    /// Registers a new user with the given credentials.
    /// </summary>
    /// <param name="registerRequest">The request object containing the credentials</param>
    /// <returns>
    /// - **201 Created** on successful creation.
    /// - **400 Bad Request** on invalid request, email already in use, or creation fails.
    /// </returns>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest registerRequest)
    {
        // Validate input
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        
        if (registerRequest.Password != registerRequest.ConfirmPassword)
            return BadRequest(new { message = "Passwords do not match" });
        
        // Check if email already exists
        var existingEmail = await _userManager.FindByEmailAsync(registerRequest.Email);
        if (existingEmail != null)
            return Conflict(new { message = "Email is already in use." });
        
        // Check if username already exists
        var existingUsername = await _userManager.FindByNameAsync(registerRequest.UserName);
        if (existingUsername != null)
            return Conflict(new { message = "Username is already in use." });
        
        // Create a new user
        var user = new IdentityUser
        {
            UserName = registerRequest.UserName,
            Email = registerRequest.Email
        };
        
        var result = await _userManager.CreateAsync(user, registerRequest.Password);
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        return Created("", new { message = "User created successfully." });
    }
    
    // POST: /api/auth/login
    /// <summary>
    /// Authenticates a users request to log in and returns a JWT token.
    /// </summary>
    /// <param name="loginRequest">User login request containing email (or username) and password</param>
    /// <returns>JWT if login request is successful.</returns>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest loginRequest)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _userManager.FindByEmailAsync(loginRequest.EmailOrUsername) ??
                   await _userManager.FindByNameAsync(loginRequest.EmailOrUsername);
        if (user == null)
            // No user found, return 401 Unauthorized
            return Unauthorized(new { message = "Email or password is incorrect." });

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, loginRequest.Password);
        if (!isPasswordValid)
            // Password is incorrect, return 401 Unauthorized
            return Unauthorized(new { message = "Email or password is incorrect." });

        var token = _jwtService.GenerateToken(user);

        // Passed all checks so return 
        return Ok(new { token });
    }
}