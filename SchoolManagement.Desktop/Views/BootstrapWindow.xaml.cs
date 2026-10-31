using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.Views;

/// <summary>
/// Splash and error surface. It intentionally contains no text: the failure may
/// be the translation layer itself. Retry is an icon-only button.
/// </summary>
public partial class BootstrapWindow : Window
{
    public BootstrapWindow()
    {
        InitializeComponent();
        ApplySystemTheme();
    }

    public event EventHandler? RetryRequested;

    public void ShowSplash()
    {
        SplashPanel.Visibility = Visibility.Visible;
        ErrorPanel.Visibility = Visibility.Collapsed;
        EnsureVisible();
    }

    public void ShowError()
    {
        SplashPanel.Visibility = Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Visible;
        EnsureVisible();
    }

    private void OnRetryClick(object sender, RoutedEventArgs e)
        => RetryRequested?.Invoke(this, EventArgs.Empty);

    private void EnsureVisible()
    {
        if (!IsVisible)
        {
            Show();
        }

        Activate();
    }

    /// <summary>
    /// The splash is shown before settings.json and the theme service exist, so
    /// it follows the Windows app theme directly.
    /// </summary>
    private void ApplySystemTheme()
    {
        var isDark = SystemTheme.IsDark();
        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isDark ? "#1A252C" : "#FAF8F5"));

        var fileName = isDark ? "splash_logo_dark.png" : "splash_logo.png";
        LogoImage.Source = new BitmapImage(
            new Uri($"pack://application:,,,/Resources/Images/{fileName}", UriKind.Absolute));
    }
}
