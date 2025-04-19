using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace MovieRecApp.Server.Models;

public class Rating
{
    [Required]
    public string UserName { get; set; }

    [Required]
    public string MovieId { get; set; }

    [Range(1, 10)]
    public float Score { get; set; }

    public Movie Movie { get; set; }
}