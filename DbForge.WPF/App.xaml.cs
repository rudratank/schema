using DbForge.Infrastructure.DependencyInjection;
using DbForge.Infrastructure.Persistence;
using DbForge.WPF.DependencyInjection;
using DbForge.WPF.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace DbForge.WPF
{
    /// <summary>
    /// Application startup and dependency injection configuration.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Static service provider for main window and global access.
        /// </summary>
        public static IServiceProvider Services { get; private set; } = null!;

        protected override void OnStartup ( StartupEventArgs e )
        {
            base.OnStartup(e);

            // ── Dependency Injection Setup ──────────────────────────────────
            var services = new ServiceCollection();

            // Add your existing layers
            services.AddDbForgeCore();
            services.AddDbForgeProviders();

            // Add WPF layer with new theme services
            services.AddDbForgeWpf();

            // Build root service provider
            var rootProvider = services.BuildServiceProvider();
            var appScope = rootProvider.CreateScope();
            Services = appScope.ServiceProvider;

            // ── Database Initialization ─────────────────────────────────────
            var dbContext = Services.GetRequiredService<AppDbContext>();
            dbContext.Database.Migrate();

            // ── Theme System Initialization ─────────────────────────────────
            // Initialize the theme service with saved preferences
            var themeService = Services.GetRequiredService<ThemeService>();
            themeService.Initialize();

            // ── Show Main Window ────────────────────────────────────────────
            Services.GetRequiredService<MainWindow>().Show();
        }
    }
}