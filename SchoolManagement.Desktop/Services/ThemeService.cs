using System.Windows;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Settings;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
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
        ApplyFluentTheme(theme);
        SetCurrentTheme(theme);
        _logger.LogInformation("Theme applied: {Theme}.", theme);
    }

    /// <summary>
    /// Keeps WPF-UI's own theme dictionary (used by the library controls that
    /// a later phase will adopt) in sync with the app palette. The accent is
    /// left untouched because the app ships its own accent tokens, and the
    /// backdrop is chosen by the window, not here.
    /// </summary>
    private static void ApplyFluentTheme(AppTheme theme)
    {
        var fluentTheme = theme == AppTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light;
        ApplicationThemeManager.Apply(fluentTheme, WindowBackdropType.None, updateAccent: false);
    }

    private void SetCurrentTheme(AppTheme theme)
    {
        if (CurrentTheme == theme)
        {
            return;
        }

        CurrentTheme = theme;
        ThemeMonitor.NotifyThemeChanged();
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }
}
