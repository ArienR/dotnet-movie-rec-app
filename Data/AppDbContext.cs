using Microsoft.EntityFrameworkCore;
using MovieRecApp.Models;

namespace MovieRecApp.Data;

public class AppDbContext : DbContext
{
    public DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlite("Data Source=Users.db");
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1, // Primary key
                Username = "admin", // Default admin username
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123") // Hashed admin password
            }
        );
    }
}