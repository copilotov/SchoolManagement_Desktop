using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Settings;
using SchoolManagement.Desktop.Controls;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>One row of the sample table in the design gallery.</summary>
public sealed record GalleryRow(string Name, string Amount, string Status, StatusTone Tone);

/// <summary>
/// Phase D1 - debug design gallery. Shows every token and control so a visual
/// regression is caught by eye before a screen is redesigned. Phase D3 adds one
/// sample per reusable component, so a control that breaks in one theme or one
/// direction is visible here first.
///
/// The gallery never changes the application language or the shell direction:
/// its two switches only preview the other theme and the other direction
/// inside the window. That keeps the preview honest (Arabic text can be shown
/// left-to-right) without disturbing the running app.
///
/// Debug only: the F12 hook in <c>ShellWindow</c> is compiled out of release
/// builds, and this type is only registered in the container for the same
/// lifetime.
/// </summary>
public partial class DesignGalleryViewModel : ObservableObject, IDisposable
{
    private readonly IThemeService _theme;
    private readonly ILocalizationService _localization;
    private bool _isSyncing;

    public DesignGalleryViewModel(IThemeService theme, ILocalizationService localization)
    {
        _theme = theme;
        _localization = localization;

        _isSyncing = true;
        try
        {
            IsDark = theme.CurrentTheme == AppTheme.Dark;
            IsRightToLeft = localization.IsRightToLeft;
            FlowDirection = IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        }
        finally
        {
            _isSyncing = false;
        }

        Rows = new ObservableCollection<GalleryRow>();
        Tags = new ObservableCollection<string>();
        MonthNames = new List<string>();
        DayLabels = new List<string>();
        WizardSteps = new List<WizardStep>();
        ScheduleItems = new List<WeekScheduleItem>();
        CompactScheduleItems = new List<WeekScheduleItem>();
        ChartLabels = new List<string>();
        ChartValues = new List<double> { 18.4, 21.2, 19.8, 26.5, 24.1, 31.7 };
        SparklineStudents = new List<double> { 18, 22, 21, 26, 30, 28, 35, 41 };
        SparklineGroups = new List<double> { 6, 7, 7, 9, 8, 10, 11, 12 };
        SparklineAttendance = new List<double> { 88, 91, 87, 93, 95, 92, 96, 94 };
        SparklineOutstanding = new List<double> { 42, 38, 40, 34, 31, 27, 24, 21 };
        SelectedMonth = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        RangeStart = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        RangeEnd = new DateTime(2026, 11, 30, 0, 0, 0, DateTimeKind.Utc);

        BuildLocalizedSamples();
        BuildRows();

        IsMotionEnabled = MotionSettings.IsMotionEnabled;

        _theme.ThemeChanged += OnThemeChanged;
        _localization.LanguageChanged += OnLanguageChanged;
        SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
    }

    public event EventHandler? CloseRequested;

    public ObservableCollection<GalleryRow> Rows { get; }

    public ObservableCollection<string> Tags { get; }

    public IReadOnlyList<string> MonthNames { get; private set; }

    public IReadOnlyList<string> DayLabels { get; private set; }

    public IReadOnlyList<WizardStep> WizardSteps { get; private set; }

    public IReadOnlyList<WeekScheduleItem> ScheduleItems { get; private set; }

    public IReadOnlyList<WeekScheduleItem> CompactScheduleItems { get; private set; }

    public IReadOnlyList<string> ChartLabels { get; private set; }

    public IReadOnlyList<double> ChartValues { get; }

    public IReadOnlyList<double> SparklineStudents { get; }

    public IReadOnlyList<double> SparklineGroups { get; }

    public IReadOnlyList<double> SparklineAttendance { get; }

    public IReadOnlyList<double> SparklineOutstanding { get; }

    [ObservableProperty]
    private bool _isDark;

    [ObservableProperty]
    private bool _isRightToLeft;

    [ObservableProperty]
    private FlowDirection _flowDirection;

    [ObservableProperty]
    private bool _isMotionEnabled;

    [ObservableProperty]
    private bool _isPanelOpen;

    [ObservableProperty]
    private bool _isDialogOpen;

    [ObservableProperty]
    private string _searchSample = string.Empty;

    [ObservableProperty]
    private DateTime? _selectedMonth;

    [ObservableProperty]
    private DateTime? _rangeStart;

    [ObservableProperty]
    private DateTime? _rangeEnd;

    public string MotionStatus => _localization.Tr(
        IsMotionEnabled ? TranslationKeys.Design.MotionOn : TranslationKeys.Design.MotionOff);

    public string ShortcutHint => _localization.Tr(TranslationKeys.Design.ShortcutHint);

    partial void OnIsDarkChanged(bool value)
    {
        if (_isSyncing)
        {
            return;
        }

        _theme.Apply(value ? AppTheme.Dark : AppTheme.Light);
    }

    partial void OnIsRightToLeftChanged(bool value)
    {
        if (_isSyncing)
        {
            return;
        }

        FlowDirection = value ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void OpenPanel() => IsPanelOpen = true;

    [RelayCommand]
    private void ClosePanel() => IsPanelOpen = false;

    [RelayCommand]
    private void OpenDialog() => IsDialogOpen = true;

    [RelayCommand]
    private void CloseDialog() => IsDialogOpen = false;

    [RelayCommand]
    private void ConfirmDialog() => IsDialogOpen = false;

    [RelayCommand]
    private void FinishWizard() => IsDialogOpen = false;

    /// <summary>Adds one sample row so the table's add button does something visible.</summary>
    [RelayCommand]
    private void AddSample()
    {
        var index = Rows.Count + 1;
        Rows.Add(new GalleryRow(
            _localization.Tr(TranslationKeys.Design.SampleStudentA)
                + " "
                + index.ToString(CultureInfo.InvariantCulture),
            (12500 + (index * 750)).ToString("N2", CultureInfo.InvariantCulture),
            _localization.Tr(TranslationKeys.Attendance.Present),
            StatusTone.Success));
    }

    [RelayCommand]
    private void RefreshSample() => BuildRows();

    /// <summary>
    /// The gallery has no file to write, so export only proves the table's
    /// export button is wired and enabled. A real screen supplies its own
    /// guarded export command.
    /// </summary>
    [RelayCommand]
    private void ExportSample()
    {
    }

    private void BuildLocalizedSamples()
    {
        MonthNames = new List<string>
        {
            _localization.Tr(TranslationKeys.Months.January),
            _localization.Tr(TranslationKeys.Months.February),
            _localization.Tr(TranslationKeys.Months.March),
            _localization.Tr(TranslationKeys.Months.April),
            _localization.Tr(TranslationKeys.Months.May),
            _localization.Tr(TranslationKeys.Months.June),
            _localization.Tr(TranslationKeys.Months.July),
            _localization.Tr(TranslationKeys.Months.August),
            _localization.Tr(TranslationKeys.Months.September),
            _localization.Tr(TranslationKeys.Months.October),
            _localization.Tr(TranslationKeys.Months.November),
            _localization.Tr(TranslationKeys.Months.December),
        };

        DayLabels = new List<string>
        {
            _localization.Tr(TranslationKeys.Days.Saturday),
            _localization.Tr(TranslationKeys.Days.Sunday),
            _localization.Tr(TranslationKeys.Days.Monday),
            _localization.Tr(TranslationKeys.Days.Tuesday),
            _localization.Tr(TranslationKeys.Days.Wednesday),
            _localization.Tr(TranslationKeys.Days.Thursday),
            _localization.Tr(TranslationKeys.Days.Friday),
        };

        ChartLabels = MonthNames.Take(6).ToList();

        var stepBody = _localization.Tr(TranslationKeys.Design.SampleBody);
        WizardSteps = new List<WizardStep>
        {
            new(_localization.Tr(TranslationKeys.Design.SampleStepA), null, stepBody),
            new(_localization.Tr(TranslationKeys.Design.SampleStepB), null, stepBody),
            new(_localization.Tr(TranslationKeys.Design.SampleStepC), null, stepBody),
        };

        Tags.Clear();
        Tags.Add(_localization.Tr(TranslationKeys.Design.SampleTagA));
        Tags.Add(_localization.Tr(TranslationKeys.Design.SampleTagB));
        Tags.Add(_localization.Tr(TranslationKeys.Design.SampleTagC));

        var group = _localization.Tr(TranslationKeys.Design.SampleTagA);
        ScheduleItems = new List<WeekScheduleItem>
        {
            Slot(0, 8, 9.5, group, StatusTone.Accent, "teacher-1"),
            Slot(0, 9.5, 11, group, StatusTone.Info, "teacher-2"),
            Slot(1, 10, 11.5, group, StatusTone.Success, "teacher-1"),
            Slot(2, 8, 9.5, group, StatusTone.Accent, "teacher-1"),
            Slot(3, 10, 11.5, group, StatusTone.Warning, "teacher-1"),
            Slot(3, 10.5, 12, group, StatusTone.Info, "teacher-1"),
            Slot(5, 9.5, 11, group, StatusTone.Success, "teacher-3"),
        };

        CompactScheduleItems = ScheduleItems.Take(3).ToList();
    }

    private static WeekScheduleItem Slot(
        int day,
        double start,
        double end,
        string group,
        StatusTone tone,
        string resourceKey)
        => new(
            day,
            TimeSpan.FromHours(start),
            TimeSpan.FromHours(end),
            group,
            TimeSpan.FromHours(start).ToString(@"hh\:mm", CultureInfo.InvariantCulture)
                + " - "
                + TimeSpan.FromHours(end).ToString(@"hh\:mm", CultureInfo.InvariantCulture),
            tone,
            resourceKey);

    private void BuildRows()
    {
        Rows.Clear();

        var students = new[]
        {
            _localization.Tr(TranslationKeys.Design.SampleStudentA),
            _localization.Tr(TranslationKeys.Design.SampleStudentB),
            _localization.Tr(TranslationKeys.Design.SampleStudentC),
        };

        var statuses = new[]
        {
            (_localization.Tr(TranslationKeys.Attendance.Present), StatusTone.Success),
            (_localization.Tr(TranslationKeys.Attendance.Late), StatusTone.Warning),
            (_localization.Tr(TranslationKeys.Attendance.Absent), StatusTone.Danger),
        };

        for (var index = 0; index < 12; index++)
        {
            var student = students[index % students.Length];
            var (status, tone) = statuses[index % statuses.Length];
            var amount = (12500 + (index * 750)).ToString("N2", CultureInfo.InvariantCulture);

            Rows.Add(new GalleryRow(
                student + " " + (index + 1).ToString(CultureInfo.InvariantCulture),
                amount,
                status,
                tone));
        }
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        _isSyncing = true;
        try
        {
            IsDark = _theme.CurrentTheme == AppTheme.Dark;
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        BuildLocalizedSamples();
        BuildRows();
        OnPropertyChanged(nameof(MonthNames));
        OnPropertyChanged(nameof(DayLabels));
        OnPropertyChanged(nameof(WizardSteps));
        OnPropertyChanged(nameof(ScheduleItems));
        OnPropertyChanged(nameof(CompactScheduleItems));
        OnPropertyChanged(nameof(ChartLabels));
        OnPropertyChanged(nameof(MotionStatus));
        OnPropertyChanged(nameof(ShortcutHint));
    }

    private void OnSystemParametersChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(SystemParameters.ClientAreaAnimation), StringComparison.Ordinal))
        {
            return;
        }

        IsMotionEnabled = MotionSettings.IsMotionEnabled;
        OnPropertyChanged(nameof(MotionStatus));
    }

    public void Dispose()
    {
        _theme.ThemeChanged -= OnThemeChanged;
        _localization.LanguageChanged -= OnLanguageChanged;
        SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
    }
}
