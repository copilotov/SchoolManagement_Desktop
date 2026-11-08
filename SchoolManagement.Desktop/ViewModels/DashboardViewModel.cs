using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Dashboard;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Time;
using SchoolManagement.Desktop.Controls;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>
/// Phase D4 - the daily command center.
///
/// It renders one immutable <see cref="DashboardSnapshot"/> loaded by a single
/// read use case, then turns it into already-localized display records: the KPI
/// row, the today's-session timeline, the prioritized attention list, the three
/// charts, the quick actions and the guided new-school setup. Every navigation
/// reuses an existing screen and hands it a ready-made filter; no write, no
/// schema change and no license rule lives here.
///
/// The layout is driven by <see cref="DashboardLayout"/> and the real content
/// width, so the page rearranges from three columns to one without overflowing.
/// </summary>
public sealed partial class DashboardViewModel : PageViewModel, IDisposable
{
    private readonly GetDashboardSnapshotUseCase _getSnapshot;
    private readonly ILocalizationService _localization;
    private readonly INavigationService _navigation;
    private readonly LicenseViewModel _license;
    private readonly ITrustedClock _clock;

    private DashboardSnapshot? _snapshot;
    private bool _isLoaded;
    private bool _disposed;

    private IReadOnlyList<double> _revenueValues = Array.Empty<double>();
    private IReadOnlyList<string> _revenueLabels = Array.Empty<string>();
    private string _revenueSummary = string.Empty;

    private IReadOnlyList<double> _attendanceValues = Array.Empty<double>();
    private IReadOnlyList<string> _attendanceLabels = Array.Empty<string>();
    private string _attendanceSummary = string.Empty;

    private IReadOnlyList<double> _levelValues = Array.Empty<double>();
    private IReadOnlyList<string> _levelLabels = Array.Empty<string>();
    private string _levelSummary = string.Empty;

    public DashboardViewModel(
        GetDashboardSnapshotUseCase getSnapshot,
        ILocalizationService localization,
        INavigationService navigation,
        LicenseViewModel license,
        ITrustedClock clock)
    {
        _getSnapshot = getSnapshot ?? throw new ArgumentNullException(nameof(getSnapshot));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _license = license ?? throw new ArgumentNullException(nameof(license));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

        Stats = new ObservableCollection<DashboardStatItem>();
        Sessions = new ObservableCollection<DashboardSessionRow>();
        Alerts = new ObservableCollection<DashboardAlertRow>();
        QuickActions = new ObservableCollection<DashboardQuickAction>();
        SetupSteps = new ObservableCollection<DashboardSetupStepItem>();

        _localization.LanguageChanged += OnLanguageChanged;
        _license.PropertyChanged += OnLicenseChanged;
    }

    public ObservableCollection<DashboardStatItem> Stats { get; }

    public ObservableCollection<DashboardSessionRow> Sessions { get; }

    public ObservableCollection<DashboardAlertRow> Alerts { get; }

    public ObservableCollection<DashboardQuickAction> QuickActions { get; }

    public ObservableCollection<DashboardSetupStepItem> SetupSteps { get; }

    public LicenseViewModel License => _license;

    [ObservableProperty]
    private UiState<DashboardSnapshot> _state = new UiState<DashboardSnapshot>.Initial();

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private int _statColumns = 3;

    [ObservableProperty]
    private int _chartColumns = 3;

    [ObservableProperty]
    private int _quickColumns = 4;

    [ObservableProperty]
    private int _attentionColumns = 2;

    /// <summary>True while the first snapshot is on its way; the page shows a spinner.</summary>
    public bool IsLoading => State is UiState<DashboardSnapshot>.Initial or UiState<DashboardSnapshot>.Loading;

    public bool HasError => State is UiState<DashboardSnapshot>.Error;

    /// <summary>The overview is hidden for a brand-new school; the guide takes over.</summary>
    public bool ShowContent => State is UiState<DashboardSnapshot>.Success && !IsNewSchool;

    public bool ShowSetup => State is UiState<DashboardSnapshot>.Success && IsNewSchool;

    public bool IsNewSchool => _snapshot?.IsNewSchool ?? false;

    public bool HasSessions => Sessions.Count > 0;

    public bool HasAlerts => Alerts.Count > 0;

    public bool HasNoAlerts => State is UiState<DashboardSnapshot>.Success && Alerts.Count == 0;

    public string SchoolName => _snapshot?.SchoolName ?? string.Empty;

    /// <summary>Morning / afternoon / evening greeting from the trusted clock.</summary>
    public string Greeting => _localization.Tr(GreetingKey());

    public string GreetingTitle => _localization.Tr(
        TranslationKeys.Dashboard.GreetingWithName,
        ("greeting", Greeting),
        ("name", SchoolName));

    /// <summary>"Saturday 7 November 2026", with localized weekday and month names.</summary>
    public string DateText
    {
        get
        {
            var date = _snapshot?.Today
                ?? DateTime.SpecifyKind(_clock.LocalNow.Date, DateTimeKind.Utc);
            return string.Concat(
                AttendanceLabels.DayName(_localization, date),
                " ",
                date.Day.ToString(CultureInfo.InvariantCulture),
                " ",
                _localization.Tr(MonthKey(date.Month)),
                " ",
                date.Year.ToString(CultureInfo.InvariantCulture));
        }
    }

    public IReadOnlyList<double> RevenueValues => _revenueValues;

    public IReadOnlyList<string> RevenueLabels => _revenueLabels;

    public string RevenueSummary => _revenueSummary;

    public IReadOnlyList<double> WeekAttendanceValues => _attendanceValues;

    public IReadOnlyList<string> WeekAttendanceLabels => _attendanceLabels;

    public string WeekAttendanceSummary => _attendanceSummary;

    public IReadOnlyList<double> LevelValues => _levelValues;

    public IReadOnlyList<string> LevelLabels => _levelLabels;

    public string LevelSummary => _levelSummary;

    /// <summary>Loads the snapshot once; a failure leaves the retry button enabled.</summary>
    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_isLoaded)
        {
            return;
        }

        _isLoaded = await LoadAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Loads the snapshot and rebuilds every display collection.</summary>
    public async Task<bool> LoadAsync(CancellationToken cancellationToken = default)
    {
        var firstLoad = _snapshot is null;
        if (firstLoad)
        {
            State = new UiState<DashboardSnapshot>.Loading();
        }

        IsRefreshing = true;
        try
        {
            var result = await _getSnapshot.ExecuteAsync(cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                ErrorMessage = result.Failure!.ToDisplayMessage(_localization);
                State = new UiState<DashboardSnapshot>.Error(result.Failure!);
                return false;
            }

            _snapshot = result.Value;
            ErrorMessage = string.Empty;
            Rebuild();
            State = new UiState<DashboardSnapshot>.Success(result.Value);
            return true;
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    /// <summary>Feeds the real content width so the page rearranges 3 -> 2 -> 1.</summary>
    public void UpdateWidth(double width)
    {
        if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
        {
            return;
        }

        StatColumns = DashboardLayout.ColumnsFor(width);
        ChartColumns = DashboardLayout.ColumnsFor(width);
        QuickColumns = DashboardLayout.QuickActionColumnsFor(width);
        AttentionColumns = DashboardLayout.AttentionColumnsFor(width);
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private async Task OpenStatAsync(DashboardStatItem? stat)
    {
        if (stat is null)
        {
            return;
        }

        switch (stat.Target)
        {
            case DashboardStatTarget.Students:
                await OpenStudentsAsync(1).ConfigureAwait(true);
                break;
            case DashboardStatTarget.Groups:
                await OpenGroupsAsync().ConfigureAwait(true);
                break;
            case DashboardStatTarget.Sessions:
                await OpenAttendanceAsync().ConfigureAwait(true);
                break;
            case DashboardStatTarget.Revenue:
            case DashboardStatTarget.Arrears:
                _navigation.NavigateToTab(ShellTab.Finance);
                break;
        }
    }

    [RelayCommand]
    private async Task OpenAlertAsync(DashboardAlertRow? alert)
    {
        if (alert is null)
        {
            return;
        }

        switch (alert.Target)
        {
            case DashboardAlertTarget.Finance:
                _navigation.NavigateToTab(ShellTab.Finance);
                break;
            case DashboardAlertTarget.Students:
                await OpenStudentsAsync(null).ConfigureAwait(true);
                break;
            case DashboardAlertTarget.Attendance:
                await OpenAttendanceAsync().ConfigureAwait(true);
                break;
            case DashboardAlertTarget.License:
                await RenewLicenseAsync().ConfigureAwait(true);
                break;
        }
    }

    [RelayCommand]
    private async Task OpenQuickActionAsync(DashboardQuickAction? action)
    {
        if (action is null)
        {
            return;
        }

        switch (action.Target)
        {
            case DashboardQuickTarget.NewStudent:
                await OpenStudentsAsync(null, openAdd: true).ConfigureAwait(true);
                break;
            case DashboardQuickTarget.Payment:
                _navigation.NavigateToTab(ShellTab.Finance);
                break;
            case DashboardQuickTarget.Attendance:
                await OpenAttendanceAsync().ConfigureAwait(true);
                break;
            case DashboardQuickTarget.Report:
                await OpenAttendanceReportAsync().ConfigureAwait(true);
                break;
        }
    }

    [RelayCommand]
    private async Task OpenSetupStepAsync(DashboardSetupStepItem? step)
    {
        if (step is null)
        {
            return;
        }

        switch (step.Target)
        {
            case DashboardSetupTarget.Levels:
                await OpenAcademicsAsync(0).ConfigureAwait(true);
                break;
            case DashboardSetupTarget.Subjects:
                await OpenAcademicsAsync(2).ConfigureAwait(true);
                break;
            case DashboardSetupTarget.Teachers:
                await OpenAcademicsAsync(3).ConfigureAwait(true);
                break;
            case DashboardSetupTarget.Groups:
                await OpenGroupsAsync().ConfigureAwait(true);
                break;
            case DashboardSetupTarget.Students:
                await OpenStudentsAsync(null).ConfigureAwait(true);
                break;
        }
    }

    /// <summary>Opens the roster of one session directly, keeping the dashboard tab.</summary>
    [RelayCommand]
    private async Task TakeAttendanceAsync(DashboardSessionRow? row)
    {
        if (row is null || !row.CanTakeAttendance)
        {
            return;
        }

        var page = _navigation.NavigateTo<TakeAttendanceViewModel>();
        if (page is null)
        {
            return;
        }

        await page.InitializeAsync(row.Id).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RenewLicenseAsync()
    {
        await _license.RenewInteractiveAsync().ConfigureAwait(true);
        await LoadAsync().ConfigureAwait(true);
    }

    private async Task OpenStudentsAsync(int? statusId, bool openAdd = false)
    {
        _navigation.NavigateToTab(ShellTab.Students);
        if (_navigation.CurrentViewModel is not StudentsViewModel page)
        {
            return;
        }

        await page.EnsureLoadedAsync().ConfigureAwait(true);

        if (statusId is { } id
            && page.Statuses.FirstOrDefault(option => option.Id == id) is { } status)
        {
            page.SelectedStatus = status;
        }

        if (openAdd)
        {
            page.AddCommand.Execute(null);
        }
    }

    [RelayCommand]
    private async Task OpenGroupsAsync()
    {
        _navigation.NavigateToTab(ShellTab.Groups);
        if (_navigation.CurrentViewModel is GroupsViewModel page)
        {
            await page.EnsureLoadedAsync().ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task OpenAttendanceAsync()
    {
        _navigation.NavigateToTab(ShellTab.Attendance);
        if (_navigation.CurrentViewModel is AttendanceViewModel page)
        {
            page.SelectedTabIndex = 0;
            await page.EnsureLoadedAsync().ConfigureAwait(true);
        }
    }

    private async Task OpenAttendanceReportAsync()
    {
        _navigation.NavigateToTab(ShellTab.Attendance);
        var page = _navigation.NavigateTo<AttendanceReportViewModel>();
        if (page is null)
        {
            return;
        }

        await page.InitializeAsync().ConfigureAwait(true);
    }

    private async Task OpenAcademicsAsync(int tabIndex)
    {
        _navigation.NavigateToTab(ShellTab.Academics);
        if (_navigation.CurrentViewModel is AcademicsViewModel page)
        {
            await page.SelectTabAsync(tabIndex).ConfigureAwait(true);
        }
    }

    private void Rebuild()
    {
        if (_snapshot is not { } snapshot)
        {
            return;
        }

        RebuildStats(snapshot);
        RebuildSessions(snapshot);
        RebuildAlerts(snapshot);
        RebuildQuickActions();
        RebuildSetup(snapshot);
        RebuildCharts(snapshot);
        RaiseComputed();
    }

    private void RebuildStats(DashboardSnapshot snapshot)
    {
        Stats.Clear();

        var currency = _localization.Tr(TranslationKeys.Common.Currency);
        var monthName = _localization.Tr(MonthKey(MonthOf(snapshot.MonthYear, snapshot.Today.Month)));
        var trend = snapshot.RevenueTrend.Select(point => point.Revenue).ToList();
        var direction = TrendDirection.Flat;
        if (trend.Count >= 2)
        {
            direction = trend[^1] > trend[^2]
                ? TrendDirection.Up
                : trend[^1] < trend[^2]
                    ? TrendDirection.Down
                    : TrendDirection.Flat;
        }

        Stats.Add(new DashboardStatItem(
            _localization.Tr(TranslationKeys.Dashboard.StudentsCount),
            FormatNumber(snapshot.ActiveStudents),
            _localization.Tr(TranslationKeys.Common.Active),
            Glyph("Icon.Students"),
            StatusTone.Accent,
            TrendDirection.Flat,
            DashboardStatTarget.Students,
            Array.Empty<double>()));

        Stats.Add(new DashboardStatItem(
            _localization.Tr(TranslationKeys.Dashboard.GroupsCount),
            FormatNumber(snapshot.ActiveGroups),
            _localization.Tr(TranslationKeys.Common.Active),
            Glyph("Icon.Groups"),
            StatusTone.Info,
            TrendDirection.Flat,
            DashboardStatTarget.Groups,
            Array.Empty<double>()));

        Stats.Add(new DashboardStatItem(
            _localization.Tr(TranslationKeys.Dashboard.TodaySessions),
            FormatNumber(snapshot.TodaySessionCount),
            _localization.Tr(TranslationKeys.Dashboard.Today),
            Glyph("Icon.Attendance"),
            StatusTone.Success,
            TrendDirection.Flat,
            DashboardStatTarget.Sessions,
            Array.Empty<double>()));

        Stats.Add(new DashboardStatItem(
            _localization.Tr(TranslationKeys.Dashboard.MonthRevenue),
            FormatMoney(snapshot.MonthRevenue, currency),
            monthName,
            Glyph("Icon.Money"),
            StatusTone.Success,
            direction,
            DashboardStatTarget.Revenue,
            trend));

        Stats.Add(new DashboardStatItem(
            _localization.Tr(TranslationKeys.Dashboard.Outstanding),
            FormatMoney(snapshot.MonthOutstanding, currency),
            _localization.Tr(
                TranslationKeys.Dashboard.ArrearsCount,
                ("count", FormatNumber(snapshot.ArrearsStudentCount))),
            Glyph("Icon.Warning"),
            StatusTone.Danger,
            TrendDirection.Flat,
            DashboardStatTarget.Arrears,
            Array.Empty<double>()));
    }


    private void RebuildSessions(DashboardSnapshot snapshot)
    {
        Sessions.Clear();

        foreach (var session in snapshot.Sessions.OrderBy(item => item.StartTime))
        {
            Sessions.Add(new DashboardSessionRow(
                session.Id,
                session.GroupName,
                string.Concat(
                    session.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture),
                    " - ",
                    session.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture)),
                AttendanceLabels.SessionType(_localization, session.Type),
                AttendanceLabels.Status(_localization, session.Status),
                _localization.Tr(
                    TranslationKeys.Dashboard.SessionStudents,
                    ("count", FormatNumber(session.StudentCount))),
                StatusBadgeMapper.MapTone(session.Status),
                session.CanTakeAttendance,
                session.HasAttendance));
        }
    }

    private void RebuildAlerts(DashboardSnapshot snapshot)
    {
        Alerts.Clear();

        var currency = _localization.Tr(TranslationKeys.Common.Currency);

        if (snapshot.ArrearsStudentCount > 0)
        {
            Alerts.Add(new DashboardAlertRow(
                _localization.Tr(TranslationKeys.Dashboard.AlertArrearsTitle),
                _localization.Tr(
                    TranslationKeys.Dashboard.AlertArrearsBody,
                    ("count", FormatNumber(snapshot.ArrearsStudentCount)),
                    ("amount", FormatMoney(snapshot.MonthOutstanding, currency))),
                _localization.Tr(TranslationKeys.Dashboard.CategoryPayments),
                _localization.Tr(TranslationKeys.Dashboard.OpenFinance),
                Glyph("Icon.Receipt"),
                StatusTone.Warning,
                DashboardAlertTarget.Finance));
        }

        if (snapshot.AbsenceAlerts.Count > 0)
        {
            Alerts.Add(new DashboardAlertRow(
                _localization.Tr(TranslationKeys.Dashboard.AlertAbsenceTitle),
                _localization.Tr(
                    TranslationKeys.Dashboard.AlertAbsenceBody,
                    ("count", FormatNumber(snapshot.AbsenceAlerts.Count)),
                    ("limit", FormatNumber(snapshot.AbsenceAlerts[0].Limit))),
                _localization.Tr(TranslationKeys.Dashboard.CategoryStudents),
                _localization.Tr(TranslationKeys.Dashboard.OpenStudents),
                Glyph("Icon.Students"),
                StatusTone.Danger,
                DashboardAlertTarget.Students));
        }

        var missingAttendance = snapshot.Sessions.Count(
            session => session.CanTakeAttendance && !session.HasAttendance);
        if (missingAttendance > 0)
        {
            Alerts.Add(new DashboardAlertRow(
                _localization.Tr(TranslationKeys.Dashboard.AlertMissingAttendanceTitle),
                _localization.Tr(
                    TranslationKeys.Dashboard.AlertMissingAttendanceBody,
                    ("count", FormatNumber(missingAttendance))),
                _localization.Tr(TranslationKeys.Dashboard.CategoryAttendance),
                _localization.Tr(TranslationKeys.Dashboard.OpenAttendance),
                Glyph("Icon.Attendance"),
                StatusTone.Info,
                DashboardAlertTarget.Attendance));
        }

        if (_license.Info.ShouldWarn)
        {
            var days = Math.Max(0, _license.Info.DaysRemaining);
            Alerts.Add(new DashboardAlertRow(
                _localization.Tr(TranslationKeys.Dashboard.AlertLicenseTitle),
                _localization.Tr(
                    TranslationKeys.Dashboard.AlertLicenseBody,
                    ("days", FormatNumber(days))),
                _localization.Tr(TranslationKeys.Dashboard.CategoryLicense),
                _localization.Tr(TranslationKeys.Dashboard.RenewLicense),
                Glyph("Icon.Lock"),
                StatusTone.Warning,
                DashboardAlertTarget.License));
        }
    }

    private void RebuildQuickActions()
    {
        QuickActions.Clear();

        QuickActions.Add(new DashboardQuickAction(
            _localization.Tr(TranslationKeys.Dashboard.QuickNewStudent),
            _localization.Tr(TranslationKeys.Dashboard.QuickNewStudentHint),
            Glyph("Icon.Add"),
            DashboardQuickTarget.NewStudent));

        QuickActions.Add(new DashboardQuickAction(
            _localization.Tr(TranslationKeys.Dashboard.QuickPayment),
            _localization.Tr(TranslationKeys.Dashboard.QuickPaymentHint),
            Glyph("Icon.Receipt"),
            DashboardQuickTarget.Payment));

        QuickActions.Add(new DashboardQuickAction(
            _localization.Tr(TranslationKeys.Dashboard.QuickAttendance),
            _localization.Tr(TranslationKeys.Dashboard.QuickAttendanceHint),
            Glyph("Icon.Attendance"),
            DashboardQuickTarget.Attendance));

        QuickActions.Add(new DashboardQuickAction(
            _localization.Tr(TranslationKeys.Dashboard.QuickReport),
            _localization.Tr(TranslationKeys.Dashboard.QuickReportHint),
            Glyph("Icon.Print"),
            DashboardQuickTarget.Report));
    }

    private void RebuildSetup(DashboardSnapshot snapshot)
    {
        SetupSteps.Clear();

        var number = 1;
        foreach (var step in snapshot.SetupSteps)
        {
            SetupSteps.Add(new DashboardSetupStepItem(
                number,
                _localization.Tr(step.Key),
                _localization.Tr(
                    TranslationKeys.Dashboard.SetupCount,
                    ("count", FormatNumber(step.Count))),
                _localization.Tr(step.IsComplete
                    ? TranslationKeys.Dashboard.SetupDone
                    : TranslationKeys.Dashboard.SetupPending),
                step.IsComplete,
                step.IsComplete ? StatusTone.Success : StatusTone.Neutral,
                TargetFor(step.Key)));
            number++;
        }
    }

    private void RebuildCharts(DashboardSnapshot snapshot)
    {
        var hasRevenue = snapshot.RevenueTrend.Any(point => point.Revenue > 0);
        _revenueValues = hasRevenue
            ? snapshot.RevenueTrend.Select(point => point.Revenue).ToList()
            : Array.Empty<double>();
        _revenueLabels = hasRevenue
            ? snapshot.RevenueTrend.Select(point => MonthLabel(point.MonthYear)).ToList()
            : Array.Empty<string>();
        _revenueSummary = hasRevenue
            ? _localization.Tr(
                TranslationKeys.Dashboard.RevenueSummary,
                ("amount", FormatMoney(
                    snapshot.RevenueTrend.Sum(point => point.Revenue),
                    _localization.Tr(TranslationKeys.Common.Currency))))
            : string.Empty;

        var hasAttendance = snapshot.WeekAttendance.Any(point => point.Total > 0);
        _attendanceValues = hasAttendance
            ? snapshot.WeekAttendance
                .Select(point => Math.Round(point.AttendanceRate, 1))
                .ToList()
            : Array.Empty<double>();
        _attendanceLabels = hasAttendance
            ? snapshot.WeekAttendance
                .Select(point => _localization.Tr(
                    WeekDayCalculator.ToTranslationKey(WeekDayCalculator.FromIndex(point.DayOfWeek))))
                .ToList()
            : Array.Empty<string>();
        var records = snapshot.WeekAttendance.Sum(point => point.Total);
        _attendanceSummary = hasAttendance && records > 0
            ? _localization.Tr(
                TranslationKeys.Dashboard.AttendanceSummary,
                ("rate", (snapshot.WeekAttendance.Sum(point => point.Present + point.Late) * 100d / records)
                    .ToString("0.#", CultureInfo.InvariantCulture)))
            : string.Empty;

        var hasLevels = snapshot.LevelDistribution.Count > 0;
        _levelValues = hasLevels
            ? snapshot.LevelDistribution.Select(slice => (double)slice.StudentCount).ToList()
            : Array.Empty<double>();
        _levelLabels = hasLevels
            ? snapshot.LevelDistribution
                .Select(slice => string.IsNullOrWhiteSpace(slice.LevelName)
                    ? _localization.Tr(TranslationKeys.Dashboard.NoLevel)
                    : slice.LevelName!)
                .ToList()
            : Array.Empty<string>();
        _levelSummary = hasLevels
            ? _localization.Tr(
                TranslationKeys.Dashboard.LevelSummary,
                ("count", FormatNumber(snapshot.LevelDistribution.Count)))
            : string.Empty;
    }

    private void RaiseComputed()
    {
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(ShowContent));
        OnPropertyChanged(nameof(ShowSetup));
        OnPropertyChanged(nameof(IsNewSchool));
        OnPropertyChanged(nameof(HasSessions));
        OnPropertyChanged(nameof(HasAlerts));
        OnPropertyChanged(nameof(HasNoAlerts));
        OnPropertyChanged(nameof(SchoolName));
        OnPropertyChanged(nameof(Greeting));
        OnPropertyChanged(nameof(GreetingTitle));
        OnPropertyChanged(nameof(DateText));
        OnPropertyChanged(nameof(RevenueValues));
        OnPropertyChanged(nameof(RevenueLabels));
        OnPropertyChanged(nameof(RevenueSummary));
        OnPropertyChanged(nameof(WeekAttendanceValues));
        OnPropertyChanged(nameof(WeekAttendanceLabels));
        OnPropertyChanged(nameof(WeekAttendanceSummary));
        OnPropertyChanged(nameof(LevelValues));
        OnPropertyChanged(nameof(LevelLabels));
        OnPropertyChanged(nameof(LevelSummary));
    }

    partial void OnStateChanged(UiState<DashboardSnapshot> value)
    {
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(ShowContent));
        OnPropertyChanged(nameof(ShowSetup));
        OnPropertyChanged(nameof(HasNoAlerts));
    }

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    private void OnLicenseChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LicenseViewModel.Info) || _snapshot is not { } snapshot)
        {
            return;
        }

        RebuildAlerts(snapshot);
        OnPropertyChanged(nameof(HasAlerts));
        OnPropertyChanged(nameof(HasNoAlerts));
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (_snapshot is null)
        {
            OnPropertyChanged(nameof(Greeting));
            OnPropertyChanged(nameof(GreetingTitle));
            OnPropertyChanged(nameof(DateText));
            return;
        }

        Rebuild();
    }

    private string GreetingKey()
    {
        var hour = _clock.LocalNow.Hour;
        if (hour is >= 5 and < 12)
        {
            return TranslationKeys.Dashboard.GreetingMorning;
        }

        return hour is >= 12 and < 17
            ? TranslationKeys.Dashboard.GreetingAfternoon
            : TranslationKeys.Dashboard.GreetingEvening;
    }

    private string MonthLabel(string monthYear)
    {
        return DateTime.TryParseExact(
            monthYear,
            "yyyy-MM",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date)
            ? _localization.Tr(MonthKey(date.Month))
            : monthYear;
    }

    private static int MonthOf(string monthYear, int fallbackMonth)
        => DateTime.TryParseExact(
            monthYear,
            "yyyy-MM",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date)
            ? date.Month
            : fallbackMonth;

    private static string MonthKey(int month) => month switch
    {
        1 => TranslationKeys.Months.January,
        2 => TranslationKeys.Months.February,
        3 => TranslationKeys.Months.March,
        4 => TranslationKeys.Months.April,
        5 => TranslationKeys.Months.May,
        6 => TranslationKeys.Months.June,
        7 => TranslationKeys.Months.July,
        8 => TranslationKeys.Months.August,
        9 => TranslationKeys.Months.September,
        10 => TranslationKeys.Months.October,
        11 => TranslationKeys.Months.November,
        _ => TranslationKeys.Months.December,
    };

    private static DashboardSetupTarget TargetFor(string key) => key switch
    {
        TranslationKeys.Dashboard.SetupLevels => DashboardSetupTarget.Levels,
        TranslationKeys.Dashboard.SetupSubjects => DashboardSetupTarget.Subjects,
        TranslationKeys.Dashboard.SetupTeachers => DashboardSetupTarget.Teachers,
        TranslationKeys.Dashboard.SetupGroups => DashboardSetupTarget.Groups,
        _ => DashboardSetupTarget.Students,
    };

    private static string FormatNumber(int value) =>
        value.ToString("N0", CultureInfo.CurrentCulture);

    private static string FormatMoney(double value, string currency) =>
        string.Concat(value.ToString("N0", CultureInfo.CurrentCulture), " ", currency);

    private static string Glyph(string key) =>
        System.Windows.Application.Current?.TryFindResource(key) as string ?? string.Empty;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _localization.LanguageChanged -= OnLanguageChanged;
        _license.PropertyChanged -= OnLicenseChanged;
        GC.SuppressFinalize(this);
    }
}
