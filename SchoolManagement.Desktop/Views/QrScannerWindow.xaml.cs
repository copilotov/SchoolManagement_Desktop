using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OpenCvSharp;
using SchoolManagement.Application.Attendance;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

/// <summary>
/// Webcam host of the QR scanner. The window only captures frames and plays the
/// confirmation sounds: duplicate prevention, group validation and roster
/// updates live in <see cref="QrScannerViewModel"/> / <see cref="QrScanSession"/>.
/// The USB reader and the manual field both type into the same capture box, so
/// no special driver is needed.
/// </summary>
public partial class QrScannerWindow : System.Windows.Window
{
    private readonly QrScannerViewModel _viewModel;
    private readonly IQrCodeDecoder _decoder;
    private readonly DispatcherTimer _timer;
    private VideoCapture? _capture;
    private string? _lastPayload;

    public QrScannerWindow(QrScannerViewModel viewModel, IQrCodeDecoder decoder)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        DataContext = viewModel;

        FlowDirection = System.Windows.Application.Current?.MainWindow?.FlowDirection ?? FlowDirection.LeftToRight;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _timer.Tick += OnTimerTick;

        _viewModel.FeedbackRequested += OnFeedbackRequested;
        _viewModel.RequestClose += OnRequestClose;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        CaptureBox.Focus();

        try
        {
            _capture = new VideoCapture(0, VideoCaptureAPIs.DSHOW);
            if (!_capture.IsOpened())
            {
                _capture.Dispose();
                _capture = null;
                _viewModel.SetCameraUnavailable();
                return;
            }

            _timer.Start();
        }
        catch (Exception)
        {
            _capture?.Dispose();
            _capture = null;
            _viewModel.SetCameraUnavailable();
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_capture is null)
        {
            return;
        }

        try
        {
            using var frame = new Mat();
            if (!_capture.Read(frame) || frame.Empty())
            {
                return;
            }

            Cv2.Flip(frame, frame, FlipMode.Y);
            var bytes = frame.ToBytes(".png");

            ShowPreview(bytes);

            var decoded = _decoder.Decode(bytes);
            if (!decoded.IsSuccess || string.IsNullOrWhiteSpace(decoded.Value))
            {
                return;
            }

            // A webcam sees the same code many times per second; report a code
            // only when the payload changes so the operator gets one clear beep.
            if (string.Equals(decoded.Value, _lastPayload, StringComparison.Ordinal))
            {
                return;
            }

            _lastPayload = decoded.Value;
            _viewModel.ScanPayload(decoded.Value);
        }
        catch (Exception)
        {
            // A frame can fail while the camera is unplugged; keep the window
            // usable through the USB reader/manual field.
            _viewModel.SetCameraUnavailable();
            _timer.Stop();
        }
    }

    private void ShowPreview(byte[] png)
    {
        using var stream = new MemoryStream(png);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        CameraImage.Source = image;
    }

    private void OnFeedbackRequested(object? sender, QrScanOutcome outcome)
    {
        if (outcome == QrScanOutcome.Accepted)
        {
            SystemSounds.Asterisk.Play();
        }
        else
        {
            SystemSounds.Hand.Play();
        }
    }

    private void OnRequestClose(object? sender, EventArgs e)
    {
        DialogResult = _viewModel.Result is not null;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _capture?.Release();
        _capture?.Dispose();
        _capture = null;

        _viewModel.FeedbackRequested -= OnFeedbackRequested;
        _viewModel.RequestClose -= OnRequestClose;
    }
}
