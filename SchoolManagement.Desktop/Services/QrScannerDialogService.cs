using System.Windows;
using SchoolManagement.Application.Attendance;
using SchoolManagement.Desktop.ViewModels;
using SchoolManagement.Desktop.Views;

namespace SchoolManagement.Desktop.Services;

/// <summary>
/// Opens the scanner modally. The service owns the WPF window so the attendance
/// ViewModel only sees <see cref="IQrScannerDialogService"/> and can be tested
/// with a substitute.
/// </summary>
public sealed class QrScannerDialogService : IQrScannerDialogService
{
    private readonly IQrCodeDecoder _decoder;

    public QrScannerDialogService(IQrCodeDecoder decoder)
    {
        _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
    }

    public Task<QrScanResult?> ScanAsync(QrScanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var viewModel = new QrScannerViewModel(request);
        var window = new QrScannerWindow(viewModel, _decoder)
        {
            Owner = System.Windows.Application.Current?.MainWindow,
        };

        window.ShowDialog();

        return Task.FromResult(
            viewModel.Result is null ? null : new QrScanResult(viewModel.Result));
    }
}
