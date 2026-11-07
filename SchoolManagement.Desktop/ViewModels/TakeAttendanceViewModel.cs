using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Attendance;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Time;
using SchoolManagement.Application.UseCases;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Policies;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>
/// One student line of the take-attendance screen. The status buttons live on
/// the row so the DataGrid template stays declarative; the parent ViewModel owns
/// the license/closed-session rule and decides whether the click is applied.
/// </summary>
public sealed partial class AttendanceStudentRow : ObservableObject
{
    private readonly Action<AttendanceStudentRow, string> _statusChanged;

    public AttendanceStudentRow(
        int studentId,
        string studentCode,
        string studentName,
        string status,
        string statusText,
        Action<AttendanceStudentRow, string> statusChanged)
    {
        StudentId = studentId;
        StudentCode = studentCode;
        StudentName = studentName;
        _status = status;
        _statusText = statusText;
        _statusChanged = statusChanged ?? throw new ArgumentNullException(nameof(statusChanged));
    }

    public int StudentId { get; }

    public string StudentCode { get; }

    public string StudentName { get; }

    [ObservableProperty]
    private string _status;

    [ObservableProperty]
    private string _statusText;

    public bool IsPresent => string.Equals(Status, AttendanceStatuses.Present, StringComparison.OrdinalIgnoreCase);

    public bool IsAbsent => AttendanceStatuses.IsAbsence(Status);

    public bool IsLate => string.Equals(Status, AttendanceStatuses.Late, StringComparison.OrdinalIgnoreCase);

    public void ApplyStatus(string status, string statusText)
    {
        Status = status;
        StatusText = statusText;
        OnPropertyChanged(nameof(IsPresent));
        OnPropertyChanged(nameof(IsAbsent));
        OnPropertyChanged(nameof(IsLate));
    }

    [RelayCommand]
    private void MarkPresent() => _statusChanged(this, AttendanceStatuses.Present);

    [RelayCommand]
    private void MarkAbsent() => _statusChanged(this, AttendanceStatuses.Absent);

    [RelayCommand]
    private void MarkLate() => _statusChanged(this, AttendanceStatuses.Late);
}

/// <summary>One homework due on the session, already localized for the list.</summary>
public sealed record HomeworkDueRow(int Id, string Title, string DueText, string CountsText);

/// <summary>
/// Take-attendance screen: one click per student, select-all helpers, the QR /
/// USB / manual scanner and the bulk guarded save. The session is completed
/// after the save, so the roster becomes read-only; the QR duplicate rule lives
/// in <see cref="QrScanSession"/> and is tested without a camera.
/// </summary>
public sealed partial class TakeAttendanceViewModel : PageViewModel, IDisposable
{
    private readonly GetSessionAttendanceBoardUseCase _getBoard;
    private readonly SaveSessionAttendanceUseCase _saveAttendance;
    private readonly ILocalizationService _localization;
    private readonly IQrScannerDialogService _scanner;
    private readonly INavigationService _navigation;
    private readonly LicenseViewModel _license;

    private SessionAttendanceBoard? _board;
    private bool _disposed;

    public TakeAttendanceViewModel(
        GetSessionAttendanceBoardUseCase getBoard,
        SaveSessionAttendanceUseCase saveAttendance,
        ILocalizationService localization,
        IQrScannerDialogService scanner,
        INavigationService navigation,
        LicenseViewModel license)
    {
        _getBoard = getBoard ?? throw new ArgumentNullException(nameof(getBoard));
        _saveAttendance = saveAttendance ?? throw new ArgumentNullException(nameof(saveAttendance));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _license = license ?? throw new ArgumentNullException(nameof(license));

        Rows = new ObservableCollection<AttendanceStudentRow>();
        DueHomeworks = new ObservableCollection<HomeworkDueRow>();

        _localization.LanguageChanged += OnLanguageChanged;
    }

    public ObservableCollection<AttendanceStudentRow> Rows { get; }

    public ObservableCollection<HomeworkDueRow> DueHomeworks { get; }

    public LicenseViewModel License => _license;

    [ObservableProperty]
    private int _sessionId;

    [ObservableProperty]
    private SessionListItem? _session;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isRenewVisible;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    public bool HasStudents => Rows.Count > 0;

    public bool HasDueHomeworks => DueHomeworks.Count > 0;

    /// <summary>Completed and cancelled sessions are terminal; their roster is read-only.</summary>
    public bool IsEditable => Session is not null && SessionStatusPolicy.CanTakeAttendance(Session.Status);

    public bool IsClosed => Session is not null && !IsEditable;

    public string GroupName => Session?.GroupName ?? string.Empty;

    public string DateText => Session is null
        ? string.Empty
        : AttendanceLabels.DayName(_localization, Session.SessionDate)
            + " "
            + Session.SessionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public string TimeText => Session is null
        ? string.Empty
        : Session.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture)
            + " - "
            + Session.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture);

    public string TypeText => AttendanceLabels.SessionType(_localization, Session?.Type);

    public string StatusText => AttendanceLabels.Status(_localization, Session?.Status);

    public string TopicText => Session?.LessonTopic ?? string.Empty;

    public int PresentCount => Rows.Count(row => row.IsPresent);

    public int AbsentCount => Rows.Count(row => row.IsAbsent);

    public int LateCount => Rows.Count(row => row.IsLate);

    public int TotalCount => Rows.Count;

    public string PresentCountText => _localization.Tr(
        TranslationKeys.Attendance.PresentCount,
        ("count", PresentCount.ToString(CultureInfo.InvariantCulture)));

    public string AbsentCountText => _localization.Tr(
        TranslationKeys.Attendance.AbsentCount,
        ("count", AbsentCount.ToString(CultureInfo.InvariantCulture)));

    public string LateCountText => _localization.Tr(
        TranslationKeys.Attendance.LateCount,
        ("count", LateCount.ToString(CultureInfo.InvariantCulture)));

    public string TotalCountText => _localization.Tr(
        TranslationKeys.Attendance.TotalCount,
        ("count", TotalCount.ToString(CultureInfo.InvariantCulture)));

    public async Task InitializeAsync(int sessionId, CancellationToken cancellationToken = default)
    {
        SessionId = sessionId;
        await LoadAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            var result = await _getBoard.ExecuteAsync(SessionId, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                ShowFailure(result.Failure!);
                return;
            }

            _board = result.Value;
            Session = result.Value.Session;
            RebuildRows(result.Value.Roster);
            RebuildHomeworks(result.Value.DueHomeworks);
            UpdateCounts();
            ErrorMessage = string.Empty;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private Task ReloadAsync() => LoadAsync();

    [RelayCommand]
    private void Back() => _navigation.NavigateTo<AttendanceViewModel>();

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!IsEditable)
        {
            ShowFailure(Failure.Validation(TranslationKeys.Attendance.SessionClosed, "status"));
            return;
        }

        if (Rows.Count == 0)
        {
            ShowFailure(Failure.Validation(TranslationKeys.Attendance.NoStudents, "students"));
            return;
        }

        var entries = Rows
            .Select(row => new AttendanceEntry(row.StudentId, row.Status))
            .ToList();

        IsBusy = true;
        try
        {
            var result = await _saveAttendance
                .ExecuteAsync(new SaveSessionAttendanceRequest(SessionId, entries))
                .ConfigureAwait(false);

            if (result.IsFailure)
            {
                ShowFailure(result.Failure!);
                return;
            }

            ShowSuccess(
                TranslationKeys.Attendance.SaveSuccess,
                ("count", result.Value.SavedCount.ToString(CultureInfo.InvariantCulture)));
            await LoadAsync().ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SetAllPresent() => ApplyToAll(AttendanceStatuses.Present);

    [RelayCommand]
    private void SetAllAbsent() => ApplyToAll(AttendanceStatuses.Absent);

    [RelayCommand]
    private void SetAllLate() => ApplyToAll(AttendanceStatuses.Late);

    [RelayCommand]
    private async Task ScanQrAsync()
    {
        if (!IsEditable)
        {
            ShowFailure(Failure.Validation(TranslationKeys.Attendance.SessionClosed, "status"));
            return;
        }

        if (Rows.Count == 0)
        {
            ShowFailure(Failure.Validation(TranslationKeys.Attendance.NoStudents, "students"));
            return;
        }

        var request = new QrScanRequest(
            Rows.Select(row => new QrScanStudent(row.StudentId, row.StudentCode, row.StudentName)).ToList(),
            _localization.Tr(TranslationKeys.Attendance.ScanQrTitle),
            _localization.Tr(TranslationKeys.Attendance.ScanHint),
            _localization.Tr(TranslationKeys.Attendance.BarcodeHint),
            _localization.Tr(TranslationKeys.Attendance.CameraUnavailable),
            _localization.Tr(TranslationKeys.Attendance.Scanned),
            _localization.Tr(TranslationKeys.Attendance.Waiting),
            _localization.Tr(TranslationKeys.Attendance.FinishScan),
            _localization.Tr(TranslationKeys.Attendance.CancelScan),
            _localization.Tr(TranslationKeys.Attendance.ScanDuplicate),
            _localization.Tr(TranslationKeys.Attendance.ScanInvalid),
            _localization.Tr(TranslationKeys.Attendance.ScanWrongGroup),
            _localization.Tr(TranslationKeys.Attendance.ScanAccepted),
            _localization.Tr(TranslationKeys.Attendance.ScanSummary));

        var result = await _scanner.ScanAsync(request).ConfigureAwait(false);
        if (result is null || result.StudentIds.Count == 0)
        {
            return;
        }

        var presentText = _localization.Tr(TranslationKeys.Attendance.Present);
        foreach (var studentId in result.StudentIds)
        {
            var row = Rows.FirstOrDefault(candidate => candidate.StudentId == studentId);
            row?.ApplyStatus(AttendanceStatuses.Present, presentText);
        }

        UpdateCounts();
        ShowSuccess(
            TranslationKeys.Attendance.ScanSummary,
            ("count", result.StudentIds.Count.ToString(CultureInfo.InvariantCulture)));
    }

    [RelayCommand]
    private async Task RenewLicenseAsync()
    {
        await _license.RenewInteractiveAsync().ConfigureAwait(false);
        await LoadAsync().ConfigureAwait(false);
    }

    partial void OnSessionChanged(SessionListItem? value)
    {
        OnPropertyChanged(nameof(IsEditable));
        OnPropertyChanged(nameof(IsClosed));
        OnPropertyChanged(nameof(GroupName));
        OnPropertyChanged(nameof(DateText));
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(TypeText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(TopicText));
    }

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    private void ApplyToAll(string status)
    {
        if (!IsEditable)
        {
            ShowFailure(Failure.Validation(TranslationKeys.Attendance.SessionClosed, "status"));
            return;
        }

        var text = AttendanceLabels.AttendanceStatus(_localization, status);
        foreach (var row in Rows)
        {
            row.ApplyStatus(status, text);
        }

        UpdateCounts();
    }

    private void ApplyStatus(AttendanceStudentRow row, string status)
    {
        if (!IsEditable)
        {
            ShowFailure(Failure.Validation(TranslationKeys.Attendance.SessionClosed, "status"));
            return;
        }

        row.ApplyStatus(status, AttendanceLabels.AttendanceStatus(_localization, status));
        UpdateCounts();
    }

    private void RebuildRows(IReadOnlyList<AttendanceRosterItem> roster)
    {
        Rows.Clear();
        foreach (var item in roster)
        {
            var status = string.IsNullOrWhiteSpace(item.Status)
                ? AttendanceStatuses.Present
                : item.Status;
            Rows.Add(new AttendanceStudentRow(
                item.StudentId,
                item.StudentCode,
                item.StudentName,
                status,
                AttendanceLabels.AttendanceStatus(_localization, status),
                ApplyStatus));
        }

        OnPropertyChanged(nameof(HasStudents));
    }

    private void RebuildHomeworks(IReadOnlyList<HomeworkListItem> homeworks)
    {
        DueHomeworks.Clear();
        foreach (var homework in homeworks)
        {
            DueHomeworks.Add(new HomeworkDueRow(
                homework.Id,
                homework.Title,
                AttendanceLabels.DayName(_localization, homework.DueDate)
                    + " "
                    + homework.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                homework.SubmittedCount.ToString(CultureInfo.InvariantCulture)
                    + "/"
                    + homework.SubmissionCount.ToString(CultureInfo.InvariantCulture)));
        }

        OnPropertyChanged(nameof(HasDueHomeworks));
    }

    private void UpdateCounts()
    {
        OnPropertyChanged(nameof(PresentCount));
        OnPropertyChanged(nameof(AbsentCount));
        OnPropertyChanged(nameof(LateCount));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(PresentCountText));
        OnPropertyChanged(nameof(AbsentCountText));
        OnPropertyChanged(nameof(LateCountText));
        OnPropertyChanged(nameof(TotalCountText));
    }

    private void ShowFailure(Failure failure)
    {
        ErrorMessage = failure.ToDisplayMessage(_localization);
        StatusMessage = string.Empty;
        IsRenewVisible = failure.Kind == FailureKind.License;
    }

    private void ShowSuccess(string messageKey, params (string Key, string Value)[] parameters)
    {
        ErrorMessage = string.Empty;
        StatusMessage = parameters.Length == 0
            ? _localization.Tr(messageKey)
            : _localization.Tr(
                messageKey,
                parameters.ToDictionary(
                    parameter => parameter.Key,
                    parameter => parameter.Value,
                    StringComparer.Ordinal));
        IsRenewVisible = false;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        foreach (var row in Rows)
        {
            row.ApplyStatus(row.Status, AttendanceLabels.AttendanceStatus(_localization, row.Status));
        }

        if (_board is not null)
        {
            RebuildHomeworks(_board.DueHomeworks);
        }

        OnPropertyChanged(nameof(DateText));
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(TypeText));
        OnPropertyChanged(nameof(StatusText));
        UpdateCounts();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _localization.LanguageChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
