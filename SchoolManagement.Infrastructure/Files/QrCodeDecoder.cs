using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Attendance;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SkiaSharp;
using ZXing;
using ZXing.Common;

namespace SchoolManagement.Infrastructure.Files;

/// <summary>
/// Decodes the QR payload of a captured frame with ZXing.Net. The desktop
/// captures webcam frames (OpenCvSharp) or re-encodes a cropped image, but the
/// imaging library stays in Infrastructure: Domain and Application only know the
/// <see cref="IQrCodeDecoder"/> port.
/// </summary>
public sealed class QrCodeDecoder : IQrCodeDecoder
{
    private readonly ILogger<QrCodeDecoder> _logger;

    public QrCodeDecoder(ILogger<QrCodeDecoder> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Result<string> Decode(byte[] imageBytes)
    {
        if (imageBytes is null || imageBytes.Length == 0)
        {
            return Result<string>.Fail(
                Failure.Validation(TranslationKeys.Attendance.ScanInvalid));
        }

        try
        {
            using var bitmap = SKBitmap.Decode(imageBytes);
            if (bitmap is null)
            {
                return Result<string>.Fail(
                    Failure.Validation(TranslationKeys.Attendance.ScanInvalid));
            }

            // ZXing's RGB source expects raw bytes, not SKBitmap, so the frame is
            // copied into a BGRA buffer. Webcam frames are small enough for this
            // copy to stay cheap.
            var colors = bitmap.Pixels;
            var pixels = new byte[colors.Length * 4];
            for (var index = 0; index < colors.Length; index++)
            {
                var color = colors[index];
                var offset = index * 4;
                pixels[offset] = color.Blue;
                pixels[offset + 1] = color.Green;
                pixels[offset + 2] = color.Red;
                pixels[offset + 3] = color.Alpha;
            }

            var source = new RGBLuminanceSource(
                pixels,
                bitmap.Width,
                bitmap.Height,
                RGBLuminanceSource.BitmapFormat.BGRA32);

            var reader = new BarcodeReaderGeneric
            {
                AutoRotate = true,
                Options = new DecodingOptions
                {
                    TryHarder = true,
                    PossibleFormats = [BarcodeFormat.QR_CODE],
                },
            };

            var result = reader.Decode(source);
            if (result is null || string.IsNullOrWhiteSpace(result.Text))
            {
                return Result<string>.Fail(
                    Failure.Validation(TranslationKeys.Attendance.ScanInvalid));
            }

            return Result<string>.Success(result.Text.Trim());
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to decode a QR frame.");
            return Result<string>.Fail(
                Failure.Unexpected(TranslationKeys.Attendance.ScanInvalid, exception.Message));
        }
    }
}
