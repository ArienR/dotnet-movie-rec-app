namespace MovieRecApp.Shared.Models;

using System.ComponentModel.DataAnnotations;

public class RegisterRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    public string UserName { get; set; } = string.Empty;
    
    [Required]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters long.")]
    public string Password { get; set; } = string.Empty;
    
    [Required]
    public string ConfirmPassword { get; set; } = string.Empty;
}