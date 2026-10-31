using Microsoft.Win32;

namespace SchoolManagement.Desktop.Services;

/// <summary>
/// Reads the Windows app theme for the splash window only. The splash is shown
/// before settings.json and the theme service are available.
/// </summary>
public static class SystemTheme
{
    public static bool IsDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }
}
