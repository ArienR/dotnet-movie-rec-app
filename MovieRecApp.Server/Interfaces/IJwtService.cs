namespace MovieRecApp.Server.Interfaces;

using Microsoft.AspNetCore.Identity;

public interface IJwtService
{
    string GenerateToken(IdentityUser user);
}