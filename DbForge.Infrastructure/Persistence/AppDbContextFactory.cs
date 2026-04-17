using DbForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext ( string[] args )
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DbForge",
            "app.db");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        optionsBuilder.UseSqlite($"Data Source={path}");

        return new AppDbContext(optionsBuilder.Options);
    }
}