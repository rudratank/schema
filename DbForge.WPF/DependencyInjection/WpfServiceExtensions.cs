using DbForge.WPF.ViewModels;
using DbForge.WPF.ViewModels.Compare;
using DbForge.WPF.Views;
using DbForge.WPF.Views.Compare;
using Microsoft.Extensions.DependencyInjection;

namespace DbForge.WPF.DependencyInjection;

public static class WpfServiceExtensions
{
    public static IServiceCollection AddDbForgeWpf ( this IServiceCollection services )
    {
        // Windows
        services.AddSingleton<MainWindow>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<ConnectionDialogViewModel>();

        // Dialogs — Transient = fresh instance every time
        services.AddTransient<ConnectionDialog>();
        services.AddTransient<CompareSetupViewModel>();
        services.AddTransient<CompareSetupWindow>();
        services.AddTransient<ConnectionSideViewModel>();
        services.AddTransient<CompareResultWindow>();
        services.AddTransient<CompareResultViewModel>();

        // CompareResultWindow is created directly in MainWindow (not DI) because
        // its ViewModel is built inline from setup results — no registration needed.

        return services;
    }
}