using DbForge.Infrastructure.DependencyInjection;
using DbForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace DbForge.UI;

public partial class App : Application
{
    protected override void OnStartup ( StartupEventArgs e )
    {
        base.OnStartup(e);

        var services = new ServiceCollection();

        // ── Your existing backend wiring ──────────────────────
        services.AddDbForgeCore();
        services.AddDbForgeProviders();

        // ── WPF ViewModels (Transient = new instance each time) ─
        services.AddTransient<MainViewModel>();
        services.AddTransient<ConnectionExplorerViewModel>();
        services.AddTransient<NewConnectionViewModel>();

        // ── WPF Windows ───────────────────────────────────────
        services.AddTransient<MainWindow>();
        services.AddTransient<NewConnectionDialog>();

        var provider = services.BuildServiceProvider();
        ServiceLocator.Initialize(provider);

        // Run EF migration on startup
        using ( var scope = provider.CreateScope() )
            scope.ServiceProvider.GetRequiredService<AppDbContext>()
                                 .Database.Migrate();

        var window = provider.GetRequiredService<MainWindow>();
        window.Show();
    }
}