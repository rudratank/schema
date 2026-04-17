using DbForge.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DbForge.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext ( DbContextOptions<AppDbContext> options ) : base(options) { }

        public DbSet<SavedConnectionEntity> SavedConnections => Set<SavedConnectionEntity>();

        /// <summary>Key-value settings table used by AppSettingsService.</summary>
        public DbSet<AppSetting> AppSettings => Set<AppSetting>();


        protected override void OnModelCreating ( ModelBuilder modelBuilder )
        {
            modelBuilder.Entity<SavedConnectionEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Name).IsRequired().HasMaxLength(200);
                e.Property(x => x.ProfileJson).IsRequired();
            });

            modelBuilder.Entity<AppSetting>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.Key).IsUnique();
                e.Property(x => x.Key).IsRequired().HasMaxLength(200);
                e.Property(x => x.Value).HasMaxLength(2000);
            });
        }
    }
}