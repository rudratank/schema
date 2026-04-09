using Microsoft.EntityFrameworkCore;

namespace DbForge.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext ( DbContextOptions<AppDbContext> options ) : base(options) { }

    public DbSet<SavedConnectionEntity> SavedConnections => Set<SavedConnectionEntity>();

    protected override void OnModelCreating ( ModelBuilder modelBuilder )
    {
        modelBuilder.Entity<SavedConnectionEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.ProfileJson).IsRequired();
        });
    }
}