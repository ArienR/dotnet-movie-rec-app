namespace MovieRecApp.Server.Controllers;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Models;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IConfiguration _configuration;

    public AuthController(UserManager<IdentityUser> userManager, IConfiguration configuration)
    {
        _userManager = userManager; // On init inject userManager
        _configuration = configuration; // On init inject appsettings.json
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
        {
            return BadRequest(ModelState);
        }
        
        // Check if email already exists
        var existingEmail = await _userManager.FindByEmailAsync(registerRequest.Email);
        if (existingEmail != null)
        {
            return BadRequest(new { message = "Email is already in use." });
        }
        
        // Check if username already exists
        var existingUsername = await _userManager.FindByNameAsync(registerRequest.UserName);
        if (existingUsername != null)
        {
            return BadRequest(new { message = "Username is already in use." });
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
        {
            return BadRequest(ModelState);
        }

        var user = await _userManager.FindByEmailAsync(loginRequest.EmailorUsername) ??
                   await _userManager.FindByNameAsync(loginRequest.EmailorUsername);
        if (user == null)
        {
            // No user found, return 401 Unauthorized
            return Unauthorized(new { message = "Email or password is incorrect." });
        }

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, loginRequest.Password);
        if (!isPasswordValid)
        {
            // Password is incorrect, return 401 Unauthorized
            return Unauthorized(new { message = "Email or password is incorrect." });
        }

        var token = GenerateJwtToken(user);

        // Passed all checks so return 
        return Ok(new { token });
    }

    /// <summary>
    /// Generates a JWT token given the authenticated users log in credentials.
    /// </summary>
    /// <param name="user">An ASP.NET IdentityUser object</param>
    /// <returns>A JWT token given the users claims.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    private string GenerateJwtToken(IdentityUser user)
    {
        // Assure all JWT configurations in appsettings.json are valid
        var keyString = _configuration["JWT:Key"] 
                  ?? throw new InvalidOperationException("JWT:Key is missing in configuration.");
        var expireMinutes = _configuration["JWT:ExpireMinutes"] 
                            ?? throw new InvalidOperationException("JWT:ExpireMinutes is missing in configuration.");
        
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(ClaimTypes.Name, user.UserName ?? throw new InvalidOperationException("UserName is null.")), // Store username explicitly
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? throw new InvalidOperationException("Email is null.")),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
        
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["JWT:Issuer"],
            audience: _configuration["JWT:Audience"],
            claims: claims,
            expires: DateTime.Now.AddMinutes(double.Parse(expireMinutes)),
            signingCredentials: creds
        );
        
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}