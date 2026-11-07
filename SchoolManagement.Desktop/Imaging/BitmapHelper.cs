using System.IO;
using System.Windows.Media.Imaging;

namespace SchoolManagement.Desktop.Imaging;

/// <summary>
/// Turns the raw PNG bytes produced by the application layer into a frozen
/// WPF image. Freezing lets the bitmap be created off the UI thread and then
/// bound safely, and <c>OnLoad</c> closes the stream immediately.
/// </summary>
public static class BitmapHelper
{
    public static BitmapSource? ToBitmapSource(byte[]? png)
    {
        if (png is null || png.Length == 0)
        {
            return null;
        }

        using var stream = new MemoryStream(png, writable: false);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
