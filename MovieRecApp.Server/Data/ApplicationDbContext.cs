using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MovieRecApp.Server.Models;

namespace MovieRecApp.Server.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
    
    public DbSet<Movie> Movies { get; set; }
    public DbSet<Rating> Ratings { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Rating>()
            .HasKey(r => new { r.UserName, r.MovieId });

        builder.Entity<Rating>()
            .HasOne(r => r.Movie)
            .WithMany(m => m.Ratings)
            .HasForeignKey(r => r.MovieId);
    }
}