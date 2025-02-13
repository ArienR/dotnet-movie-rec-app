namespace MovieRecApp.Shared.Models;

using System.ComponentModel.DataAnnotations;

public class LoginRequest
{
    [Required]
    public string EmailOrUsername { get; set; } = string.Empty;
    
    [Required]
    public string Password { get; set; } = string.Empty;
}