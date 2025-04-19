using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MovieRecApp.Server.Models;

public class Movie
{
    [Key]
    public string MovieId { get; set; }

    [Required]
    public string Title { get; set; }

    public string? PosterUrl { get; set; }

    public ICollection<Rating> Ratings { get; set; }
}