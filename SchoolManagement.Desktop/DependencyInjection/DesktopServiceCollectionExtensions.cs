using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.ViewModels;
using SchoolManagement.Desktop.Views;

namespace SchoolManagement.Desktop.DependencyInjection;

public static class DesktopServiceCollectionExtensions
{
    /// <summary>
    /// Registers WPF services and ViewModels. ViewModels are transient; the
    /// NavigationService caches one instance per tab for state preservation.
    /// Shared services (theme, navigation, localization, settings) are singletons.
    /// </summary>
    public static IServiceCollection AddDesktop(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<INavigationService, NavigationService>();

        services.AddSingleton<ShellViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<StudentsViewModel>();
        services.AddTransient<GroupsViewModel>();
        services.AddTransient<FinanceViewModel>();
        services.AddTransient<SettingsViewModel>();

        services.AddTransient<ShellWindow>();

        return services;
    }
}
