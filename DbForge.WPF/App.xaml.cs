using DbForge.Infrastructure.DependencyInjection;
using DbForge.Infrastructure.Persistence;
using DbForge.WPF.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace DbForge.WPF
{

    public partial class App : Application
    {
        // Static so MainWindow.xaml.cs can call App.Services.GetRequiredService<T>()
        public static IServiceProvider Services { get; private set; } = null!;

        protected override void OnStartup ( StartupEventArgs e )
        {
            base.OnStartup(e);
            //DI setup
            var services = new ServiceCollection();

            // Your existing layers — zero changes needed there
            services.AddDbForgeCore();
            services.AddDbForgeProviders();

            // WPF layer
            services.AddDbForgeWpf();

            // Build a long-lived scope (replaces HTTP request scope for desktop apps)
            var rootProvider = services.BuildServiceProvider();
            var appScope = rootProvider.CreateScope();
            Services = appScope.ServiceProvider;

            // Run SQLite migrations (same as your old Program.cs)
            Services.GetRequiredService<AppDbContext>().Database.Migrate();

            Services.GetRequiredService<MainWindow>().Show();
        }
    }
}
