using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Attendance;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Groups;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Time;
using SchoolManagement.Application.UseCases;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Policies;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>One group of a filter or editor ComboBox. A null id means "all".</summary>
public sealed record SessionGroupOption(int? Id, string Label);

/// <summary>One choice of the extra-session type ComboBox.</summary>
public sealed record SessionTypeOption(string Value, string Label);

/// <summary>One choice of the payment method ComboBox.</summary>
public sealed record PaymentMethodOption(string Value, string Label);

/// <summary>
/// One line of the attendance table. It stores the already-localized labels so
/// the DataGrid has no converters, and the list rebuilds it on a language change.
/// </summary>
public sealed record SessionRow(
    int Id,
    int GroupId,
    string GroupName,
    DateTime SessionDate,
    string DateText,
    string TimeText,
    string Type,
    string TypeText,
    string Status,
    string StatusText,
    string? LessonTopic,
    string? CancelReason,
    string? CancelReasonText,
    string PaymentText,
    bool IsPaid,
    bool IsExempted,
    bool HasPrice,
    double PaidAmount,
    double RemainingAmount)
{
    public bool CanTakeAttendance => SessionStatusPolicy.CanTakeAttendance(Status);

    public bool IsExtraSession =>
        string.Equals(Type, SessionTypes.Extra, StringComparison.OrdinalIgnoreCase)
        || string.Equals(Type, SessionTypes.Makeup, StringComparison.OrdinalIgnoreCase);

    public bool CanPay => IsExtraSession && HasPrice && !IsPaid && !IsExempted;

    public bool IsCancelled => string.Equals(Status, SessionStatuses.Cancelled, StringComparison.OrdinalIgnoreCase);

    public bool IsPostponed => string.Equals(Status, SessionStatuses.Postponed, StringComparison.OrdinalIgnoreCase);

    public bool IsCompleted => string.Equals(Status, SessionStatuses.Completed, StringComparison.OrdinalIgnoreCase);

    public bool CanCancel => !IsCompleted && !IsCancelled;

    public bool CanPostpone => !IsCompleted && !IsPostponed;

    public bool HasCancelReason => !string.IsNullOrWhiteSpace(CancelReason);

    public string TopicText => LessonTopic ?? string.Empty;

    public static SessionRow Create(SessionListItem item, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(localization);

        var price = item.Price;
        var hasPrice = price is > 0;
        var paymentText = !hasPrice
            ? string.Empty
            : item.PaymentStatus is { Length: > 0 } status
                ? AttendanceLabels.PaymentStatus(localization, status)
                : item.IsPaid
                    ? localization.Tr(TranslationKeys.Attendance.Paid)
                    : localization.Tr(TranslationKeys.Attendance.Unpaid);

        var reasonText = string.IsNullOrWhiteSpace(item.CancelReason)
            ? null
            : localization.Tr(
                TranslationKeys.Attendance.CancelReasonDisplay,
                ("reason", item.CancelReason.Trim()));

        return new SessionRow(
            item.Id,
            item.GroupId,
            item.GroupName,
            item.SessionDate,
            AttendanceLabels.DayName(localization, item.SessionDate)
                + " "
                + item.SessionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            item.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture)
                + " - "
                + item.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture),
            item.Type,
            AttendanceLabels.SessionType(localization, item.Type),
            item.Status,
            AttendanceLabels.Status(localization, item.Status),
            item.LessonTopic,
            item.CancelReason,
            reasonText,
            paymentText,
            item.IsPaid,
            string.Equals(item.PaymentStatus, PaymentStatuses.Exempted, StringComparison.OrdinalIgnoreCase),
            hasPrice,
            item.PaidAmount,
            item.RemainingAmount);
    }
}

/// <summary>
/// Attendance screen: today's sessions and all sessions, session generation, the
/// makeup/extra session form, cancel/postpone/restore with the optional SMS
/// suggestion, and the pay/exempt action of an extra session. Every write is a
/// guarded use case; the SMS text is recorded first and the desktop only opens
/// the messaging application.
/// </summary>
public sealed partial class AttendanceViewModel : PageViewModel, IDisposable
{
    private readonly GetSessionListUseCase _getSessions;
    private readonly GetGroupsUseCase _getGroups;
    private readonly GenerateSessionsUseCase _generateSessions;
    private readonly CreateExtraSessionUseCase _createExtraSession;
    private readonly ChangeSessionStatusUseCase _changeStatus;
    private readonly SetSessionPaymentUseCase _setPayment;
    private readonly SendSessionSmsUseCase _sendSms;
    private readonly ILocalizationService _localization;
    private readonly IUserDialogService _dialogs;
    private readonly ISmsLauncher _smsLauncher;
    private readonly INavigationService _navigation;
    private readonly ITrustedClock _clock;
    private readonly LicenseViewModel _license;

    private IReadOnlyList<SessionListItem> _lastItems = Array.Empty<SessionListItem>();
    private string? _pendingStatus;
    private int? _pendingSessionId;
    private int? _paymentSessionId;
    private string _paymentMode = SessionPaymentModes.Paid;
    private bool _suppressReload;
    private bool _isLoaded;
    private bool _disposed;

    public AttendanceViewModel(
        GetSessionListUseCase getSessions,
        GetGroupsUseCase getGroups,
        GenerateSessionsUseCase generateSessions,
        CreateExtraSessionUseCase createExtraSession,
        ChangeSessionStatusUseCase changeStatus,
        SetSessionPaymentUseCase setPayment,
        SendSessionSmsUseCase sendSms,
        ILocalizationService localization,
        IUserDialogService dialogs,
        ISmsLauncher smsLauncher,
        INavigationService navigation,
        ITrustedClock clock,
        LicenseViewModel license)
    {
        _getSessions = getSessions ?? throw new ArgumentNullException(nameof(getSessions));
        _getGroups = getGroups ?? throw new ArgumentNullException(nameof(getGroups));
        _generateSessions = generateSessions ?? throw new ArgumentNullException(nameof(generateSessions));
        _createExtraSession = createExtraSession ?? throw new ArgumentNullException(nameof(createExtraSession));
        _changeStatus = changeStatus ?? throw new ArgumentNullException(nameof(changeStatus));
        _setPayment = setPayment ?? throw new ArgumentNullException(nameof(setPayment));
        _sendSms = sendSms ?? throw new ArgumentNullException(nameof(sendSms));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _smsLauncher = smsLauncher ?? throw new ArgumentNullException(nameof(smsLauncher));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _license = license ?? throw new ArgumentNullException(nameof(license));

        Items = new ObservableCollection<SessionRow>();
        Groups = new ObservableCollection<SessionGroupOption>();
        ExtraGroups = new ObservableCollection<SessionGroupOption>();
        SessionTypes = new ObservableCollection<SessionTypeOption>();
        PaymentMethods = new ObservableCollection<PaymentMethodOption>();

        SelectedDate = Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        BuildStaticOptions();
        _localization.LanguageChanged += OnLanguageChanged;
    }

    public ObservableCollection<SessionRow> Items { get; }

    /// <summary>Filter options: "all groups" first, then every group.</summary>
    public ObservableCollection<SessionGroupOption> Groups { get; }

    /// <summary>Group choices of the extra-session form (no "all" entry).</summary>
    public ObservableCollection<SessionGroupOption> ExtraGroups { get; }

    public ObservableCollection<SessionTypeOption> SessionTypes { get; }

    public ObservableCollection<PaymentMethodOption> PaymentMethods { get; }

    public LicenseViewModel License => _license;

    /// <summary>Business "today": the trusted local clock, never the raw device clock.</summary>
    public DateTime Today => DateTime.SpecifyKind(_clock.LocalNow.Date, DateTimeKind.Utc);

    [ObservableProperty]
    private UiState<IReadOnlyList<SessionRow>> _state = new UiState<IReadOnlyList<SessionRow>>.Initial();

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private string _selectedDate = string.Empty;

    [ObservableProperty]
    private SessionGroupOption? _selectedGroup;

    [ObservableProperty]
    private SessionRow? _selectedItem;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isRenewVisible;

    [ObservableProperty]
    private bool _isExtraSessionOpen;

    [ObservableProperty]
    private SessionGroupOption? _selectedExtraGroup;

    [ObservableProperty]
    private string _extraDate = string.Empty;

    [ObservableProperty]
    private string _extraStartTime = "08:00";

    [ObservableProperty]
    private string _extraEndTime = "09:00";

    [ObservableProperty]
    private SessionTypeOption? _selectedExtraType;

    [ObservableProperty]
    private string _extraPrice = "0";

    [ObservableProperty]
    private string _extraTopic = string.Empty;

    [ObservableProperty]
    private string _extraNotes = string.Empty;

    [ObservableProperty]
    private bool _isStatusOverlayOpen;

    [ObservableProperty]
    private string _statusOverlayTitle = string.Empty;

    [ObservableProperty]
    private string _statusReason = string.Empty;

    [ObservableProperty]
    private bool _isPaymentOverlayOpen;

    [ObservableProperty]
    private string _paymentOverlayTitle = string.Empty;

    [ObservableProperty]
    private string _paymentAmount = "0";

    [ObservableProperty]
    private PaymentMethodOption? _selectedPaymentMethod;

    [ObservableProperty]
    private bool _isPaidMode = true;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    public bool IsEmpty => State is UiState<IReadOnlyList<SessionRow>>.Empty;

    public bool IsLoading => State.IsLoading;

    public bool IsTodayTab => SelectedTabIndex == 0;

    public string EmptyMessage => IsTodayTab
        ? _localization.Tr(TranslationKeys.Attendance.NoSessionsToday)
        : _localization.Tr(TranslationKeys.Attendance.NoSessions);

    /// <summary>Builds the query sent to the filtered session read use case.</summary>
    public SessionListQuery CreateQuery()
    {
        var groupId = SelectedGroup?.Id;

        return IsTodayTab
            ? new SessionListQuery(ParseSelectedDate() ?? Today, ParseSelectedDate() ?? Today, groupId)
            : new SessionListQuery(GroupId: groupId);
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
        State = new UiState<IReadOnlyList<SessionRow>>.Loading();
        var result = await _getSessions.ExecuteAsync(CreateQuery(), cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            _lastItems = Array.Empty<SessionListItem>();
            Items.Clear();
            State = new UiState<IReadOnlyList<SessionRow>>.Error(result.Failure!);
            ShowFailure(result.Failure!);
            return;
        }

        _lastItems = result.Value;
        RebuildRows();
        State = _lastItems.Count == 0
            ? new UiState<IReadOnlyList<SessionRow>>.Empty()
            : new UiState<IReadOnlyList<SessionRow>>.Success(Items);
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>Parses the selected date; a malformed value is treated as "today".</summary>
    public DateTime? ParseSelectedDate()
    {
        if (DateTime.TryParseExact(
                SelectedDate?.Trim(),
                new[] { "yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy" },
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
        }

        return null;
    }

    [RelayCommand]
    private Task SearchAsync() => RefreshAsync();

    [RelayCommand]
    private Task ReloadAsync() => RefreshAsync();

    [RelayCommand]
    private async Task GenerateSessionsAsync()
    {
        var date = ParseSelectedDate();
        if (date is null)
        {
            ShowFailure(Failure.Validation(TranslationKeys.Validators.InvalidDate, "date"));
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _generateSessions
                .ExecuteAsync(new SessionGenerationRequest(date.Value, SelectedGroup?.Id))
                .ConfigureAwait(false);

            if (result.IsFailure)
            {
                ShowFailure(result.Failure!);
                return;
            }

            if (result.Value.CreatedCount > 0)
            {
                ShowSuccess(
                    TranslationKeys.Groups.GenerationCreated,
                    ("count", result.Value.CreatedCount.ToString(CultureInfo.InvariantCulture)));
            }
            else if (result.Value.AlreadyExisted > 0)
            {
                ShowSuccess(TranslationKeys.Groups.GenerationAlreadyExists);
            }
            else
            {
                ShowSuccess(TranslationKeys.Groups.GenerationNone);
            }

            await RefreshAsync().ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenExtraSession()
    {
        ClearMessages();
        SelectedExtraGroup = ExtraGroups.FirstOrDefault();
        ExtraDate = ParseSelectedDate()?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            ?? Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        ExtraStartTime = "08:00";
        ExtraEndTime = "09:00";
        SelectedExtraType = SessionTypes.FirstOrDefault();
        ExtraPrice = "0";
        ExtraTopic = string.Empty;
        ExtraNotes = string.Empty;
        IsExtraSessionOpen = true;
    }

    [RelayCommand]
    private void CancelExtraSession() => IsExtraSessionOpen = false;

    [RelayCommand]
    private async Task SaveExtraSessionAsync()
    {
        if (!IsExtraSessionOpen || SelectedExtraGroup?.Id is not > 0)
        {
            ShowFailure(Failure.Validation(TranslationKeys.Validators.Required, "group"));
            return;
        }

        var date = ParseDate(ExtraDate);
        if (date is null)
        {
            ShowFailure(Failure.Validation(TranslationKeys.Validators.InvalidDate, "sessionDate"));
            return;
        }

        if (!ScheduleTimeParser.TryParse(ExtraStartTime, out var start)
            || !ScheduleTimeParser.TryParse(ExtraEndTime, out var end))
        {
            ShowFailure(Failure.Validation(TranslationKeys.Attendance.InvalidTimeRange, "endTime"));
            return;
        }

        var price = double.TryParse(
            ExtraPrice,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsedPrice)
            ? parsedPrice
            : 0;

        var request = new ExtraSessionRequest(
            SelectedExtraGroup.Id!.Value,
            date.Value,
            start,
            end,
            SelectedExtraType?.Value ?? SchoolManagement.Domain.Common.SessionTypes.Extra,
            string.IsNullOrWhiteSpace(ExtraTopic) ? null : ExtraTopic.Trim(),
            string.IsNullOrWhiteSpace(ExtraNotes) ? null : ExtraNotes.Trim(),
            price);

        IsBusy = true;
        try
        {
            var result = await _createExtraSession.ExecuteAsync(request).ConfigureAwait(false);
            if (result.IsFailure)
            {
                ShowFailure(result.Failure!);
                return;
            }

            IsExtraSessionOpen = false;
            ShowSuccess(result.Value.Created
                ? TranslationKeys.Attendance.SessionCreated
                : TranslationKeys.Attendance.SessionExists);
            await RefreshAsync().ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task TakeAttendanceAsync(SessionRow? row)
    {
        var target = row ?? SelectedItem;
        if (target is null)
        {
            return;
        }

        if (!target.CanTakeAttendance)
        {
            ShowFailure(Failure.Validation(TranslationKeys.Attendance.SessionClosed, "status"));
            return;
        }

        var page = _navigation.NavigateTo<TakeAttendanceViewModel>();
        await page.InitializeAsync(target.Id).ConfigureAwait(false);
    }

    [RelayCommand]
    private Task TakeSelectedAttendanceAsync() => TakeAttendanceAsync(SelectedItem);

    [RelayCommand]
    private async Task StartSessionAsync(SessionRow? row)
    {
        var target = row ?? SelectedItem;
        if (target is null)
        {
            return;
        }

        await ChangeStatusAsync(target.Id, SessionStatuses.InProgress, null, TranslationKeys.Attendance.SessionUpdated)
            .ConfigureAwait(false);
    }

    [RelayCommand]
    private void CancelSession(SessionRow? row) => OpenStatusOverlay(row, SessionStatuses.Cancelled, TranslationKeys.Attendance.CancelSession);

    [RelayCommand]
    private void PostponeSession(SessionRow? row) => OpenStatusOverlay(row, SessionStatuses.Postponed, TranslationKeys.Attendance.PostponeSession);

    [RelayCommand]
    private async Task RestoreSessionAsync(SessionRow? row)
    {
        var target = row ?? SelectedItem;
        if (target is null)
        {
            return;
        }

        await ChangeStatusAsync(target.Id, SessionStatuses.Scheduled, null, TranslationKeys.Attendance.SessionRestored)
            .ConfigureAwait(false);
    }

    [RelayCommand]
    private void CancelStatusOverlay()
    {
        IsStatusOverlayOpen = false;
        StatusReason = string.Empty;
        _pendingStatus = null;
        _pendingSessionId = null;
    }

    [RelayCommand]
    private async Task ConfirmStatusAsync()
    {
        var sessionId = _pendingSessionId;
        var status = _pendingStatus;
        if (sessionId is null || status is null)
        {
            return;
        }

        var reason = status == SessionStatuses.Cancelled ? StatusReason : null;
        var messageKey = status switch
        {
            SessionStatuses.Cancelled => TranslationKeys.Attendance.SessionCancelled,
            SessionStatuses.Postponed => TranslationKeys.Attendance.SessionPostponed,
            _ => TranslationKeys.Attendance.SessionUpdated,
        };

        IsStatusOverlayOpen = false;
        StatusReason = string.Empty;
        _pendingStatus = null;
        _pendingSessionId = null;

        await ChangeStatusAsync(sessionId.Value, status, reason, messageKey).ConfigureAwait(false);
    }

    [RelayCommand]
    private void OpenPay(SessionRow? row) => OpenPaymentOverlay(row, SessionPaymentModes.Paid);

    [RelayCommand]
    private void OpenExempt(SessionRow? row) => OpenPaymentOverlay(row, SessionPaymentModes.Exempted);

    [RelayCommand]
    private void CancelPayment() => IsPaymentOverlayOpen = false;

    [RelayCommand]
    private async Task ConfirmPaymentAsync()
    {
        if (_paymentSessionId is not { } sessionId)
        {
            return;
        }

        var amount = double.TryParse(
            PaymentAmount,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsedAmount)
            ? parsedAmount
            : 0;

        var request = new SessionPaymentRequest(
            sessionId,
            _paymentMode,
            amount,
            SelectedPaymentMethod?.Value ?? SchoolManagement.Domain.Common.PaymentMethods.Cash);

        IsBusy = true;
        try
        {
            var result = await _setPayment.ExecuteAsync(request).ConfigureAwait(false);
            if (result.IsFailure)
            {
                ShowFailure(result.Failure!);
                return;
            }

            IsPaymentOverlayOpen = false;
            ShowSuccess(TranslationKeys.Attendance.PaymentSaved);
            await RefreshAsync().ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenHomeworksAsync()
    {
        var page = _navigation.NavigateTo<HomeworkViewModel>();
        await page.InitializeAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task OpenReportAsync()
    {
        var page = _navigation.NavigateTo<AttendanceReportViewModel>();
        await page.InitializeAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task RenewLicenseAsync()
    {
        await _license.RenewInteractiveAsync().ConfigureAwait(false);
        await RefreshAsync().ConfigureAwait(false);
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsTodayTab));
        OnPropertyChanged(nameof(EmptyMessage));
        _ = RefreshIfLoadedAsync();
    }

    partial void OnSelectedGroupChanged(SessionGroupOption? value) => _ = RefreshIfLoadedAsync();

    partial void OnSelectedDateChanged(string value) => _ = RefreshIfLoadedAsync();

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    partial void OnIsPaidModeChanged(bool value) => OnPropertyChanged(nameof(IsPaidMode));

    private async Task ChangeStatusAsync(int sessionId, string status, string? reason, string successKey)
    {
        IsBusy = true;
        try
        {
            var result = await _changeStatus
                .ExecuteAsync(new SessionStatusChangeRequest(sessionId, status, reason))
                .ConfigureAwait(false);

            if (result.IsFailure)
            {
                ShowFailure(result.Failure!);
                return;
            }

            ShowSuccess(successKey);
            await RefreshAsync().ConfigureAwait(false);

            if (result.Value.SuggestsSms)
            {
                await SuggestSmsAsync(sessionId, result.Value.Status).ConfigureAwait(false);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SuggestSmsAsync(int sessionId, string eventCode)
    {
        var confirmed = await _dialogs.ConfirmAsync(
            _localization.Tr(TranslationKeys.Attendance.Title),
            _localization.Tr(TranslationKeys.Attendance.SmsSuggestion),
            _localization.Tr(TranslationKeys.Common.Yes),
            _localization.Tr(TranslationKeys.Common.No)).ConfigureAwait(false);

        if (!confirmed)
        {
            return;
        }

        var logged = await _sendSms
            .ExecuteAsync(new SessionSmsRequest(sessionId, eventCode))
            .ConfigureAwait(false);

        if (logged.IsFailure)
        {
            ShowFailure(logged.Failure!);
            return;
        }

        var opened = await _smsLauncher
            .OpenAsync(logged.Value.Phones, logged.Value.Body)
            .ConfigureAwait(false);

        if (!opened)
        {
            ShowFailure(Failure.Unexpected(TranslationKeys.Attendance.SmsOpenFailed));
            return;
        }

        ShowSuccess(
            TranslationKeys.Attendance.SmsSent,
            ("count", logged.Value.LogCount.ToString(CultureInfo.InvariantCulture)));
    }

    private void OpenStatusOverlay(SessionRow? row, string status, string titleKey)
    {
        var target = row ?? SelectedItem;
        if (target is null)
        {
            return;
        }

        ClearMessages();
        _pendingSessionId = target.Id;
        _pendingStatus = status;
        StatusReason = target.CancelReason ?? string.Empty;
        StatusOverlayTitle = _localization.Tr(titleKey);
        IsStatusOverlayOpen = true;
    }

    private void OpenPaymentOverlay(SessionRow? row, string mode)
    {
        var target = row ?? SelectedItem;
        if (target is null)
        {
            return;
        }

        ClearMessages();
        _paymentSessionId = target.Id;
        _paymentMode = mode;
        IsPaidMode = string.Equals(mode, SessionPaymentModes.Paid, StringComparison.OrdinalIgnoreCase);
        PaymentOverlayTitle = _localization.Tr(
            IsPaidMode ? TranslationKeys.Attendance.Pay : TranslationKeys.Attendance.Exempt);
        PaymentAmount = (target.RemainingAmount > 0 ? target.RemainingAmount : 0)
            .ToString("0.##", CultureInfo.InvariantCulture);
        SelectedPaymentMethod = PaymentMethods.FirstOrDefault();
        IsPaymentOverlayOpen = true;
    }

    private void RebuildRows()
    {
        var selectedId = SelectedItem?.Id;
        Items.Clear();
        foreach (var item in _lastItems)
        {
            Items.Add(SessionRow.Create(item, _localization));
        }

        SelectedItem = selectedId is null
            ? Items.FirstOrDefault()
            : Items.FirstOrDefault(row => row.Id == selectedId) ?? Items.FirstOrDefault();
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
            var previousExtra = SelectedExtraGroup?.Id;

            Groups.Clear();
            Groups.Add(new SessionGroupOption(null, _localization.Tr(TranslationKeys.Students.AllGroups)));
            ExtraGroups.Clear();
            foreach (var group in result.Value)
            {
                Groups.Add(new SessionGroupOption(group.Id, group.Name));
                ExtraGroups.Add(new SessionGroupOption(group.Id, group.Name));
            }

            SelectedGroup = Groups.FirstOrDefault(option => option.Id == previous) ?? Groups[0];
            SelectedExtraGroup = ExtraGroups.FirstOrDefault(option => option.Id == previousExtra)
                ?? ExtraGroups.FirstOrDefault();
        }
        finally
        {
            _suppressReload = false;
        }
    }

    private async Task RefreshIfLoadedAsync()
    {
        if (_suppressReload || !_isLoaded)
        {
            return;
        }

        await RefreshAsync().ConfigureAwait(false);
    }

    private void BuildStaticOptions()
    {
        var previousType = SelectedExtraType?.Value;
        var previousMethod = SelectedPaymentMethod?.Value;

        SessionTypes.Clear();
        SessionTypes.Add(new SessionTypeOption(
            SchoolManagement.Domain.Common.SessionTypes.Extra,
            _localization.Tr(TranslationKeys.Attendance.Extra)));
        SessionTypes.Add(new SessionTypeOption(
            SchoolManagement.Domain.Common.SessionTypes.Makeup,
            _localization.Tr(TranslationKeys.Attendance.Makeup)));
        SelectedExtraType = SessionTypes.FirstOrDefault(option => option.Value == previousType)
            ?? SessionTypes.FirstOrDefault();

        PaymentMethods.Clear();
        PaymentMethods.Add(new PaymentMethodOption(
            SchoolManagement.Domain.Common.PaymentMethods.Cash,
            _localization.Tr(TranslationKeys.PaymentMethods.Cash)));
        PaymentMethods.Add(new PaymentMethodOption(
            SchoolManagement.Domain.Common.PaymentMethods.BankTransfer,
            _localization.Tr(TranslationKeys.PaymentMethods.BankTransfer)));
        PaymentMethods.Add(new PaymentMethodOption(
            SchoolManagement.Domain.Common.PaymentMethods.Check,
            _localization.Tr(TranslationKeys.PaymentMethods.Check)));
        PaymentMethods.Add(new PaymentMethodOption(
            SchoolManagement.Domain.Common.PaymentMethods.Other,
            _localization.Tr(TranslationKeys.PaymentMethods.Other)));
        SelectedPaymentMethod = PaymentMethods.FirstOrDefault(option => option.Value == previousMethod)
            ?? PaymentMethods.FirstOrDefault();
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
        BuildStaticOptions();
        if (Groups.Count > 0)
        {
            Groups[0] = new SessionGroupOption(null, _localization.Tr(TranslationKeys.Students.AllGroups));
        }

        RebuildRows();
        OnPropertyChanged(nameof(EmptyMessage));
        if (IsStatusOverlayOpen)
        {
            StatusOverlayTitle = _localization.Tr(
                _pendingStatus == SessionStatuses.Postponed
                    ? TranslationKeys.Attendance.PostponeSession
                    : TranslationKeys.Attendance.CancelSession);
        }

        if (IsPaymentOverlayOpen)
        {
            PaymentOverlayTitle = _localization.Tr(
                IsPaidMode ? TranslationKeys.Attendance.Pay : TranslationKeys.Attendance.Exempt);
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
