using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Attendance;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Groups;
using SchoolManagement.Application.Homeworks;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Time;
using SchoolManagement.Application.UseCases;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Policies;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>One session choice of the homework editor.</summary>
public sealed record HomeworkSessionOption(int Id, string Label);

/// <summary>One line of the homework report, already localized for the DataGrid.</summary>
public sealed record HomeworkRow(
    int Id,
    int GroupId,
    int SessionId,
    int? TargetSessionId,
    string Title,
    string Description,
    DateTime DueDate,
    string DueText,
    string? TargetSessionText,
    string CountsText,
    int SubmissionCount,
    int SubmittedCount,
    int PendingCount,
    bool IsCompleted,
    string StatusText)
{
    public static HomeworkRow Create(
        HomeworkListItem item,
        ILocalizationService localization,
        IReadOnlyDictionary<int, string> sessionLabels)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(localization);

        sessionLabels.TryGetValue(item.TargetSessionId ?? 0, out var targetLabel);

        return new HomeworkRow(
            item.Id,
            item.GroupId,
            item.SessionId,
            item.TargetSessionId,
            item.Title,
            item.Description,
            item.DueDate,
            AttendanceLabels.DayName(localization, item.DueDate)
                + " "
                + item.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            targetLabel,
            item.SubmittedCount.ToString(CultureInfo.InvariantCulture)
                + " / "
                + item.SubmissionCount.ToString(CultureInfo.InvariantCulture),
            item.SubmissionCount,
            item.SubmittedCount,
            item.PendingCount,
            item.IsCompleted,
            localization.Tr(
                item.IsCompleted ? TranslationKeys.Homework.Submitted : TranslationKeys.Homework.Pending));
    }
}

/// <summary>
/// One student line of the submissions overlay. The four status buttons call back
/// into the parent so the license gate and the reload flow stay in one place.
/// </summary>
public sealed partial class HomeworkSubmissionRow : ObservableObject
{
    private readonly Func<HomeworkSubmissionRow, string, Task> _statusChanged;

    public HomeworkSubmissionRow(
        int studentId,
        string studentCode,
        string studentName,
        string status,
        string statusText,
        string submittedText,
        Func<HomeworkSubmissionRow, string, Task> statusChanged)
    {
        StudentId = studentId;
        StudentCode = studentCode;
        StudentName = studentName;
        _status = status;
        _statusText = statusText;
        _submittedText = submittedText;
        _statusChanged = statusChanged ?? throw new ArgumentNullException(nameof(statusChanged));
    }

    public int StudentId { get; }

    public string StudentCode { get; }

    public string StudentName { get; }

    [ObservableProperty]
    private string _status;

    [ObservableProperty]
    private string _statusText;

    [ObservableProperty]
    private string _submittedText;

    public bool IsSubmitted => HomeworkPolicy.CountsAsSubmitted(Status);

    public bool IsPending => string.Equals(Status, HomeworkStatuses.Pending, StringComparison.OrdinalIgnoreCase);

    public bool IsMissing => string.Equals(Status, HomeworkStatuses.Missing, StringComparison.OrdinalIgnoreCase);

    public void ApplyStatus(string status, string statusText, string submittedText)
    {
        Status = status;
        StatusText = statusText;
        SubmittedText = submittedText;
        OnPropertyChanged(nameof(IsSubmitted));
        OnPropertyChanged(nameof(IsPending));
        OnPropertyChanged(nameof(IsMissing));
    }

    [RelayCommand]
    private Task MarkSubmittedAsync() => _statusChanged(this, HomeworkStatuses.Submitted);

    [RelayCommand]
    private Task MarkLateAsync() => _statusChanged(this, HomeworkStatuses.Late);

    [RelayCommand]
    private Task MarkMissingAsync() => _statusChanged(this, HomeworkStatuses.Missing);

    [RelayCommand]
    private Task MarkPendingAsync() => _statusChanged(this, HomeworkStatuses.Pending);
}

/// <summary>
/// Homework report screen: group/period filters, the shared add/edit overlay and
/// the per-homework submissions overlay (pending/submitted/late/missing). Every
/// write is a guarded use case; the report read never touches the license.
/// </summary>
public sealed partial class HomeworkViewModel : PageViewModel, IDisposable
{
    private readonly GetHomeworkReportUseCase _getReport;
    private readonly GetHomeworkSubmissionBoardUseCase _getBoard;
    private readonly SaveHomeworkUseCase _saveHomework;
    private readonly SetHomeworkSubmissionStatusUseCase _setSubmission;
    private readonly GetGroupsUseCase _getGroups;
    private readonly GetSessionListUseCase _getSessions;
    private readonly ILocalizationService _localization;
    private readonly INavigationService _navigation;
    private readonly ITrustedClock _clock;
    private readonly LicenseViewModel _license;

    private IReadOnlyList<HomeworkListItem> _lastItems = Array.Empty<HomeworkListItem>();
    private readonly Dictionary<int, string> _sessionLabels = new();
    private IReadOnlyList<HomeworkSubmissionItem> _lastSubmissions = Array.Empty<HomeworkSubmissionItem>();
    private bool _suppressReload;
    private bool _isLoaded;
    private bool _disposed;

    public HomeworkViewModel(
        GetHomeworkReportUseCase getReport,
        GetHomeworkSubmissionBoardUseCase getBoard,
        SaveHomeworkUseCase saveHomework,
        SetHomeworkSubmissionStatusUseCase setSubmission,
        GetGroupsUseCase getGroups,
        GetSessionListUseCase getSessions,
        ILocalizationService localization,
        INavigationService navigation,
        ITrustedClock clock,
        LicenseViewModel license)
    {
        _getReport = getReport ?? throw new ArgumentNullException(nameof(getReport));
        _getBoard = getBoard ?? throw new ArgumentNullException(nameof(getBoard));
        _saveHomework = saveHomework ?? throw new ArgumentNullException(nameof(saveHomework));
        _setSubmission = setSubmission ?? throw new ArgumentNullException(nameof(setSubmission));
        _getGroups = getGroups ?? throw new ArgumentNullException(nameof(getGroups));
        _getSessions = getSessions ?? throw new ArgumentNullException(nameof(getSessions));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _license = license ?? throw new ArgumentNullException(nameof(license));

        Items = new ObservableCollection<HomeworkRow>();
        Groups = new ObservableCollection<SessionGroupOption>();
        EditorGroups = new ObservableCollection<SessionGroupOption>();
        EditorSessions = new ObservableCollection<HomeworkSessionOption>();
        TargetSessions = new ObservableCollection<HomeworkSessionOption>();
        SubmissionRows = new ObservableCollection<HomeworkSubmissionRow>();

        FromDate = FirstDayOfMonth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        ToDate = Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        _localization.LanguageChanged += OnLanguageChanged;
    }

    public ObservableCollection<HomeworkRow> Items { get; }

    public ObservableCollection<SessionGroupOption> Groups { get; }

    public ObservableCollection<SessionGroupOption> EditorGroups { get; }

    public ObservableCollection<HomeworkSessionOption> EditorSessions { get; }

    public ObservableCollection<HomeworkSessionOption> TargetSessions { get; }

    public ObservableCollection<HomeworkSubmissionRow> SubmissionRows { get; }

    public LicenseViewModel License => _license;

    public string Title => _localization.Tr(TranslationKeys.Homework.ReportTitle);

    public DateTime Today => DateTime.SpecifyKind(_clock.LocalNow.Date, DateTimeKind.Utc);

    public DateTime FirstDayOfMonth => new(Today.Year, Today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    [ObservableProperty]
    private UiState<IReadOnlyList<HomeworkRow>> _state = new UiState<IReadOnlyList<HomeworkRow>>.Initial();

    [ObservableProperty]
    private SessionGroupOption? _selectedGroup;

    [ObservableProperty]
    private string _fromDate = string.Empty;

    [ObservableProperty]
    private string _toDate = string.Empty;

    [ObservableProperty]
    private HomeworkRow? _selectedItem;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isRenewVisible;

    [ObservableProperty]
    private bool _isEditorOpen;

    [ObservableProperty]
    private string _editorTitle = string.Empty;

    [ObservableProperty]
    private int? _editingId;

    [ObservableProperty]
    private SessionGroupOption? _selectedEditorGroup;

    [ObservableProperty]
    private HomeworkSessionOption? _selectedEditorSession;

    [ObservableProperty]
    private HomeworkSessionOption? _selectedTargetSession;

    [ObservableProperty]
    private string _editorTitleText = string.Empty;

    [ObservableProperty]
    private string _editorDescription = string.Empty;

    [ObservableProperty]
    private string _editorDueDate = string.Empty;

    [ObservableProperty]
    private string _editorNotes = string.Empty;

    [ObservableProperty]
    private string _editorSessionsHint = string.Empty;

    [ObservableProperty]
    private bool _isSubmissionsOpen;

    [ObservableProperty]
    private string _submissionsTitle = string.Empty;

    [ObservableProperty]
    private string _submissionsSummary = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    public bool IsEmpty => State is UiState<IReadOnlyList<HomeworkRow>>.Empty;

    public bool IsLoading => State.IsLoading;

    public bool HasSubmissions => SubmissionRows.Count > 0;

    public bool HasNoSubmissions => SubmissionRows.Count == 0;

    public bool HasEditorSessions => EditorSessions.Count > 0;

    public bool HasNoEditorSessions => EditorSessions.Count == 0;

    public string GroupFilterText => SelectedGroup?.Label ?? string.Empty;

    /// <summary>Builds the report query from the filter bar.</summary>
    public HomeworkReportQuery CreateQuery() => new(
        SelectedGroup?.Id,
        ParseDate(FromDate),
        ParseDate(ToDate));

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_isLoaded)
        {
            return;
        }

        await LoadGroupsAsync(cancellationToken).ConfigureAwait(false);
        await RefreshAsync(cancellationToken).ConfigureAwait(false);
        _isLoaded = true;
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        State = new UiState<IReadOnlyList<HomeworkRow>>.Loading();
        var result = await _getReport.ExecuteAsync(CreateQuery(), cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            _lastItems = Array.Empty<HomeworkListItem>();
            Items.Clear();
            State = new UiState<IReadOnlyList<HomeworkRow>>.Error(result.Failure!);
            ShowFailure(result.Failure!);
            return;
        }

        _lastItems = result.Value.Homeworks;
        RebuildRows();
        State = _lastItems.Count == 0
            ? new UiState<IReadOnlyList<HomeworkRow>>.Empty()
            : new UiState<IReadOnlyList<HomeworkRow>>.Success(Items);
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private Task SearchAsync() => RefreshAsync();

    [RelayCommand]
    private Task ReloadAsync() => RefreshAsync();

    [RelayCommand]
    private void Back() => _navigation.NavigateTo<AttendanceViewModel>();

    [RelayCommand]
    private async Task AddAsync()
    {
        ClearMessages();
        ResetEditor();
        EditorTitle = _localization.Tr(TranslationKeys.Homework.Add);
        EditorDueDate = Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        SelectedEditorGroup = EditorGroups.FirstOrDefault(option => option.Id == SelectedGroup?.Id)
            ?? EditorGroups.FirstOrDefault();
        IsEditorOpen = true;
        await LoadEditorSessionsAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task EditAsync(HomeworkRow? row)
    {
        var target = row ?? SelectedItem;
        if (target is null)
        {
            return;
        }

        ClearMessages();
        ResetEditor();
        EditorTitle = _localization.Tr(TranslationKeys.Homework.Edit);
        EditingId = target.Id;
        EditorTitleText = target.Title;
        EditorDescription = target.Description;
        EditorDueDate = target.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        SelectedEditorGroup = EditorGroups.FirstOrDefault(option => option.Id == target.GroupId)
            ?? EditorGroups.FirstOrDefault();
        IsEditorOpen = true;
        await LoadEditorSessionsAsync().ConfigureAwait(false);

        SelectedEditorSession = EditorSessions.FirstOrDefault(option => option.Id == target.SessionId)
            ?? EditorSessions.FirstOrDefault();
        SelectedTargetSession = TargetSessions.FirstOrDefault(option => option.Id == target.TargetSessionId);
    }

    [RelayCommand]
    private void CancelEditor()
    {
        IsEditorOpen = false;
        ResetEditor();
    }

    [RelayCommand]
    private async Task SaveEditorAsync()
    {
        if (!IsEditorOpen)
        {
            return;
        }

        if (SelectedEditorGroup?.Id is not > 0)
        {
            ShowFailure(Failure.Validation(TranslationKeys.Validators.Required, "groupId"));
            return;
        }

        if (SelectedEditorSession?.Id is not > 0)
        {
            ShowFailure(Failure.Validation(
                TranslationKeys.Homework.NoSessionsAvailable,
                "sessionId"));
            return;
        }

        var dueDate = ParseDate(EditorDueDate);
        if (dueDate is null)
        {
            ShowFailure(Failure.Validation(TranslationKeys.Validators.InvalidDate, "dueDate"));
            return;
        }

        var request = new HomeworkSaveRequest(
            SelectedEditorGroup.Id.Value,
            SelectedEditorSession.Id,
            SelectedTargetSession?.Id,
            EditorTitleText,
            EditorDescription,
            dueDate.Value,
            EditorNotes,
            EditingId);

        IsBusy = true;
        try
        {
            var result = await _saveHomework.ExecuteAsync(request).ConfigureAwait(false);
            if (result.IsFailure)
            {
                ShowFailure(result.Failure!);
                return;
            }

            IsEditorOpen = false;
            ResetEditor();
            ShowSuccess(result.Value.Created
                ? TranslationKeys.Homework.AddedSuccess
                : TranslationKeys.Homework.UpdatedSuccess);
            await RefreshAsync().ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenSubmissionsAsync(HomeworkRow? row)
    {
        var target = row ?? SelectedItem;
        if (target is null)
        {
            return;
        }

        ClearMessages();
        IsBusy = true;
        try
        {
            var result = await _getBoard.ExecuteAsync(target.Id).ConfigureAwait(false);
            if (result.IsFailure)
            {
                ShowFailure(result.Failure!);
                return;
            }

            SubmissionsTitle = result.Value.Homework.Title;
            _lastSubmissions = result.Value.Submissions;
            RebuildSubmissions();
            IsSubmissionsOpen = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CloseSubmissions()
    {
        IsSubmissionsOpen = false;
        SubmissionRows.Clear();
        _lastSubmissions = Array.Empty<HomeworkSubmissionItem>();
        OnPropertyChanged(nameof(HasSubmissions));
        OnPropertyChanged(nameof(HasNoSubmissions));
    }

    [RelayCommand]
    private async Task RenewLicenseAsync()
    {
        await _license.RenewInteractiveAsync().ConfigureAwait(false);
        if (IsSubmissionsOpen)
        {
            var selected = SelectedItem;
            if (selected is not null)
            {
                await OpenSubmissionsAsync(selected).ConfigureAwait(false);
            }
        }
        else
        {
            await RefreshAsync().ConfigureAwait(false);
        }
    }

    partial void OnSelectedGroupChanged(SessionGroupOption? value)
    {
        OnPropertyChanged(nameof(GroupFilterText));
        _ = RefreshIfLoadedAsync();
    }

    partial void OnFromDateChanged(string value) => _ = RefreshIfLoadedAsync();

    partial void OnToDateChanged(string value) => _ = RefreshIfLoadedAsync();

    partial void OnSelectedEditorGroupChanged(SessionGroupOption? value)
    {
        if (IsEditorOpen)
        {
            _ = LoadEditorSessionsAsync();
        }
    }

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    private async Task SetSubmissionAsync(HomeworkSubmissionRow row, string status)
    {
        if (SelectedItem is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _setSubmission
                .ExecuteAsync(new HomeworkSubmissionRequest(SelectedItem.Id, row.StudentId, status))
                .ConfigureAwait(false);

            if (result.IsFailure)
            {
                ShowFailure(result.Failure!);
                return;
            }

            ShowSuccess(TranslationKeys.Homework.SubmissionSaved);
            await ReloadSubmissionsAsync(SelectedItem.Id).ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ReloadSubmissionsAsync(int homeworkId)
    {
        var result = await _getBoard.ExecuteAsync(homeworkId).ConfigureAwait(false);
        if (result.IsFailure)
        {
            ShowFailure(result.Failure!);
            return;
        }

        _lastSubmissions = result.Value.Submissions;
        RebuildSubmissions();
        await RefreshAsync().ConfigureAwait(false);
    }

    private async Task LoadGroupsAsync(CancellationToken cancellationToken)
    {
        var result = await _getGroups
            .ExecuteAsync(new GroupListQuery(IncludeInactive: true), cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            ShowFailure(result.Failure!);
            return;
        }

        _suppressReload = true;
        try
        {
            var previous = SelectedGroup?.Id;
            var previousEditor = SelectedEditorGroup?.Id;

            Groups.Clear();
            Groups.Add(new SessionGroupOption(null, _localization.Tr(TranslationKeys.Homework.AllGroups)));
            EditorGroups.Clear();
            foreach (var group in result.Value)
            {
                Groups.Add(new SessionGroupOption(group.Id, group.Name));
                EditorGroups.Add(new SessionGroupOption(group.Id, group.Name));
            }

            SelectedGroup = Groups.FirstOrDefault(option => option.Id == previous) ?? Groups[0];
            SelectedEditorGroup = EditorGroups.FirstOrDefault(option => option.Id == previousEditor)
                ?? EditorGroups.FirstOrDefault();
        }
        finally
        {
            _suppressReload = false;
        }
    }

    private async Task LoadEditorSessionsAsync()
    {
        EditorSessions.Clear();
        TargetSessions.Clear();
        TargetSessions.Add(new HomeworkSessionOption(0, _localization.Tr(TranslationKeys.Homework.NoSession)));

        if (SelectedEditorGroup?.Id is not { } groupId)
        {
            EditorSessionsHint = _localization.Tr(TranslationKeys.Homework.NoSessionsAvailable);
            OnPropertyChanged(nameof(HasEditorSessions));
        OnPropertyChanged(nameof(HasNoEditorSessions));
            return;
        }

        var result = await _getSessions
            .ExecuteAsync(new SessionListQuery(GroupId: groupId))
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            ShowFailure(result.Failure!);
            return;
        }

        var ordered = result.Value
            .OrderByDescending(session => session.SessionDate)
            .ThenByDescending(session => session.StartTime)
            .ToList();

        foreach (var session in ordered)
        {
            var label = AttendanceLabels.DayName(_localization, session.SessionDate)
                + " "
                + session.SessionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                + " "
                + session.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture);
            EditorSessions.Add(new HomeworkSessionOption(session.Id, label));
            TargetSessions.Add(new HomeworkSessionOption(session.Id, label));
            _sessionLabels[session.Id] = label;
        }

        SelectedEditorSession ??= EditorSessions.FirstOrDefault();
        EditorSessionsHint = EditorSessions.Count == 0
            ? _localization.Tr(TranslationKeys.Homework.NoSessionsAvailable)
            : string.Empty;
        OnPropertyChanged(nameof(HasEditorSessions));
        OnPropertyChanged(nameof(HasNoEditorSessions));
    }

    private void RebuildRows()
    {
        var selectedId = SelectedItem?.Id;
        Items.Clear();
        foreach (var item in _lastItems)
        {
            Items.Add(HomeworkRow.Create(item, _localization, _sessionLabels));
        }

        SelectedItem = selectedId is null
            ? Items.FirstOrDefault()
            : Items.FirstOrDefault(row => row.Id == selectedId) ?? Items.FirstOrDefault();
    }

    private void RebuildSubmissions()
    {
        SubmissionRows.Clear();
        foreach (var submission in _lastSubmissions)
        {
            SubmissionRows.Add(new HomeworkSubmissionRow(
                submission.StudentId,
                submission.StudentCode,
                submission.StudentName,
                submission.Status,
                AttendanceLabels.HomeworkStatus(_localization, submission.Status),
                submission.SubmittedAt is { } submittedAt
                    ? submittedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                    : string.Empty,
                SetSubmissionAsync));
        }

        // A ratio ("3 / 5") carries no words, so it needs no translation key.
        SubmissionsSummary = SubmissionRows.Count(row => row.IsSubmitted)
            .ToString(CultureInfo.InvariantCulture)
            + " / "
            + SubmissionRows.Count.ToString(CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(HasSubmissions));
        OnPropertyChanged(nameof(HasNoSubmissions));
    }

    private void ResetEditor()
    {
        EditingId = null;
        EditorTitleText = string.Empty;
        EditorDescription = string.Empty;
        EditorNotes = string.Empty;
        EditorDueDate = string.Empty;
        EditorSessions.Clear();
        TargetSessions.Clear();
        EditorSessionsHint = string.Empty;
        SelectedEditorSession = null;
        SelectedTargetSession = null;
        OnPropertyChanged(nameof(HasEditorSessions));
        OnPropertyChanged(nameof(HasNoEditorSessions));
    }

    private async Task RefreshIfLoadedAsync()
    {
        if (_suppressReload || !_isLoaded)
        {
            return;
        }

        await RefreshAsync().ConfigureAwait(false);
    }

    private static DateTime? ParseDate(string? value)
    {
        if (DateTime.TryParseExact(
                value?.Trim(),
                new[] { "yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy" },
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
        }

        return null;
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

    private void ClearMessages()
    {
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        IsRenewVisible = false;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Title));
        if (Groups.Count > 0)
        {
            Groups[0] = new SessionGroupOption(null, _localization.Tr(TranslationKeys.Homework.AllGroups));
        }

        RebuildRows();
        RebuildSubmissions();
        if (IsEditorOpen)
        {
            EditorTitle = _localization.Tr(
                EditingId is null ? TranslationKeys.Homework.Add : TranslationKeys.Homework.Edit);
        }
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
