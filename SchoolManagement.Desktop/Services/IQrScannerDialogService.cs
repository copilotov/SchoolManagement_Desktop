namespace SchoolManagement.Desktop.Services;

/// <summary>
/// MVVM seam over the QR scanning window. The ViewModel passes the roster and
/// localized labels, receives the accepted student ids, and never references a
/// webcam, ZXing or WPF directly, so it stays testable without a message loop.
/// </summary>
public interface IQrScannerDialogService
{
    Task<QrScanResult?> ScanAsync(QrScanRequest request);
}
