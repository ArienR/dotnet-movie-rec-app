using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MovieRecApp.Server.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MovieRecApp.Server.Services;

public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Generates a JWT token given the authenticated users log in credentials.
    /// </summary>
    /// <param name="user">An ASP.NET IdentityUser object</param>
    /// <returns>A JWT token given the users claims.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public string GenerateToken(IdentityUser user)
    {
        var key = _configuration["JWT:Key"] 
                  ?? throw new InvalidOperationException("JWT:Key is missing in configuration.");
        var expireMinutes = _configuration["JWT:ExpireMinutes"] 
                            ?? throw new InvalidOperationException("JWT:ExpireMinutes is missing in configuration.");

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(ClaimTypes.Name, user.UserName ?? throw new InvalidOperationException("Username is null.")),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? throw new InvalidOperationException("Email is null.")),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var keyBytes = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var creds = new SigningCredentials(keyBytes, SecurityAlgorithms.HmacSha256);

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