using Microsoft.EntityFrameworkCore;
using presentationSiteApi.Models;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<SpotifyToken> SpotifyTokens { get; set; }
}