using System.Windows;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Settings;
using WpfApplication = System.Windows.Application;

namespace SchoolManagement.Desktop.Services;

public sealed class ThemeService : IThemeService
{
    private readonly ILogger<ThemeService> _logger;

    public ThemeService(ILogger<ThemeService> logger)
    {
        _logger = logger;
    }

    public AppTheme CurrentTheme { get; private set; } = AppTheme.Light;

    public event EventHandler? ThemeChanged;

    public void Apply(AppTheme theme)
    {
        if (WpfApplication.Current is not { } application)
        {
            SetCurrentTheme(theme);
            return;
        }

        if (application.Dispatcher.CheckAccess())
        {
            ApplyCore(application, theme);
        }
        else
        {
            application.Dispatcher.Invoke(() => ApplyCore(application, theme));
        }
    }

    private void ApplyCore(WpfApplication application, AppTheme theme)
    {
        var source = new Uri(
            $"pack://application:,,,/SchoolManagement.Desktop;component/Resources/Themes/{theme}.xaml",
            UriKind.Absolute);

        var dictionary = new ResourceDictionary { Source = source };
        var merged = application.Resources.MergedDictionaries;
        var existing = merged.FirstOrDefault(
            item => item.Source?.OriginalString.Contains("Resources/Themes/", StringComparison.OrdinalIgnoreCase) == true);

        if (existing is not null)
        {
            merged.Remove(existing);
        }

        merged.Insert(0, dictionary);
        SetCurrentTheme(theme);
        _logger.LogInformation("Theme applied: {Theme}.", theme);
    }

    private void SetCurrentTheme(AppTheme theme)
    {
        if (CurrentTheme == theme)
        {
            return;
        }

        CurrentTheme = theme;
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }
}
