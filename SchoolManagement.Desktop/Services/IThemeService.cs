using SchoolManagement.Application.Settings;

namespace SchoolManagement.Desktop.Services;

public interface IThemeService
{
    AppTheme CurrentTheme { get; }

    event EventHandler? ThemeChanged;

    /// <summary>Swaps the theme ResourceDictionary in place; no restart required.</summary>
    void Apply(AppTheme theme);
}
