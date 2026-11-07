using SchoolManagement.Application.Common;

namespace SchoolManagement.Application.Attendance;

/// <summary>
/// Decodes a QR payload from a captured frame. The desktop captures webcam
/// frames (OpenCvSharp) and the USB reader types into a text field; the image
/// decoder itself lives in Infrastructure (ZXing.Net + SkiaSharp), so Domain and
/// Application never reference an imaging library.
/// </summary>
public interface IQrCodeDecoder
{
    Result<string> Decode(byte[] imageBytes);
}

/// <summary>PDF and Excel exports of the attendance report.</summary>
public interface IAttendanceReportService
{
    Task<Result<byte[]>> BuildPdfAsync(
        AttendanceReportData data,
        CancellationToken cancellationToken = default);

    Task<Result<byte[]>> BuildExcelAsync(
        AttendanceReportData data,
        CancellationToken cancellationToken = default);
}

/// <summary>One SMS attempt. The app never uses a gateway: it opens the messaging app.</summary>
public sealed record SmsSendRequest(
    string Phone,
    string Body,
    string? EventCode = null,
    int? RecipientId = null,
    string? RecipientName = null,
    int? NotificationId = null);

public sealed record SmsSendResult(int LogId, string Phone, string Body);

/// <summary>
/// Records an SMS attempt in <c>sms_logs</c>. Opening the messaging application
/// is a desktop concern (an <c>sms:</c> URI), so the Application port only knows
/// the log row and the normalized phone.
/// </summary>
public interface ISmsService
{
    Task<Result<SmsSendResult>> SendAsync(
        SmsSendRequest request,
        CancellationToken cancellationToken = default);
}
