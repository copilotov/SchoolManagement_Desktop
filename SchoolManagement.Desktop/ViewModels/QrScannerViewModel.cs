using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>One roster line of the scanner window.</summary>
public sealed partial class QrScanRosterRow : ObservableObject
{
    public QrScanRosterRow(QrScanStudent student, string waitingLabel)
    {
        StudentId = student.StudentId;
        StudentCode = student.StudentCode;
        StudentName = student.StudentName;
        StatusText = waitingLabel;
    }

    public int StudentId { get; }

    public string StudentCode { get; }

    public string StudentName { get; }

    [ObservableProperty]
    private bool _isScanned;

    [ObservableProperty]
    private string _statusText;
}

/// <summary>
/// The scanning screen: webcam frames and the USB reader both feed the same
/// <see cref="QrScanSession"/>, which enforces duplicate prevention and rejects
/// a code that does not belong to the group. The window owns the camera and the
/// confirmation sounds; this ViewModel owns the rules and stays testable.
/// </summary>
public sealed partial class QrScannerViewModel : ObservableObject
{
    private readonly QrScanSession _session;
    private readonly QrScanRequest _request;
    private readonly Dictionary<int, QrScanRosterRow> _rows = new();

    public QrScannerViewModel(QrScanRequest request)
    {
        _request = request ?? throw new ArgumentNullException(nameof(request));
        _session = new QrScanSession(request.Students);

        Title = request.Title;
        Hint = request.Hint;
        ManualHint = request.ManualHint;
        CameraUnavailableText = request.CameraUnavailableText;
        ScannedLabel = request.ScannedLabel;
        WaitingLabel = request.WaitingLabel;
        FinishLabel = request.FinishLabel;
        CancelLabel = request.CancelLabel;
        ScannedCountTemplate = request.ScannedCountTemplate;

        Roster = new ObservableCollection<QrScanRosterRow>(
            request.Students.Select(student => new QrScanRosterRow(student, request.WaitingLabel)));

        foreach (var row in Roster)
        {
            _rows[row.StudentId] = row;
        }
    }

    public string Title { get; }

    public string Hint { get; }

    public string ManualHint { get; }

    public string CameraUnavailableText { get; }

    public string ScannedLabel { get; }

    public string WaitingLabel { get; }

    public string FinishLabel { get; }

    public string CancelLabel { get; }

    public string ScannedCountTemplate { get; }

    public ObservableCollection<QrScanRosterRow> Roster { get; }

    public IReadOnlyList<int>? Result { get; private set; }

    public event EventHandler<QrScanOutcome>? FeedbackRequested;

    public event EventHandler? RequestClose;

    [ObservableProperty]
    private string _captureInput = string.Empty;

    [ObservableProperty]
    private string _lastMessage = string.Empty;

    [ObservableProperty]
    private bool _isError;

    [ObservableProperty]
    private bool _isCameraAvailable = true;

    [ObservableProperty]
    private string _scannedCountText = string.Empty;

    public bool HasMessage => !string.IsNullOrEmpty(LastMessage);

    public void UpdateScannedCount()
        => ScannedCountText = string.Format(
            CultureInfo.InvariantCulture,
            ScannedCountTemplate,
            _session.Count);

    public void SetCameraUnavailable()
    {
        IsCameraAvailable = false;
        LastMessage = CameraUnavailableText;
        IsError = false;
    }

    /// <summary>Called by the camera loop and by the text field.</summary>
    public QrScanOutcome ScanPayload(string? payload)
    {
        var outcome = _session.Scan(payload, out var student);
        IsError = outcome is not QrScanOutcome.Accepted;
        LastMessage = outcome switch
        {
            QrScanOutcome.Accepted => FormatAccepted(student!),
            QrScanOutcome.Duplicate => _request.DuplicateMessage,
            QrScanOutcome.NotInGroup => _request.WrongGroupMessage,
            _ => _request.InvalidMessage,
        };

        if (outcome == QrScanOutcome.Accepted && student is not null && _rows.TryGetValue(student.StudentId, out var row))
        {
            row.IsScanned = true;
            row.StatusText = _request.ScannedLabel;
        }

        UpdateScannedCount();
        FeedbackRequested?.Invoke(this, outcome);
        OnPropertyChanged(nameof(HasMessage));
        return outcome;
    }

    [RelayCommand]
    private void SubmitInput()
    {
        var value = CaptureInput;
        CaptureInput = string.Empty;
        if (!string.IsNullOrWhiteSpace(value))
        {
            ScanPayload(value);
        }
    }

    [RelayCommand]
    private void Finish()
    {
        Result = _session.AcceptedStudentIds;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel()
    {
        Result = null;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private string FormatAccepted(QrScanStudent student)
        => _request.AcceptedMessageTemplate
            .Replace("{name}", student.StudentName, StringComparison.Ordinal)
            .Replace("{code}", student.StudentCode, StringComparison.Ordinal);
}
