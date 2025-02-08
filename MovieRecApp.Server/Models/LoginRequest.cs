namespace MovieRecApp.Server.Models;

using System.ComponentModel.DataAnnotations;

public class LoginRequest
{
    [Required]
    public string EmailorUsername { get; set; }
    
    [Required]
    public string Password { get; set; }
}