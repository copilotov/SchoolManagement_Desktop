using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Groups;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.UseCases;
using SchoolManagement.Desktop.Controls;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>
/// Read-only group file: capacity, subject/teacher, enrolled students, weekly
/// slots and attendance counters. The shortcuts jump to enrollment, schedule
/// and the printable report without leaving the Phase 6 flow.
/// </summary>
public sealed partial class GroupDetailsViewModel : PageViewModel, IDisposable
{
    private readonly GetGroupDetailsUseCase _getDetails;
    private readonly ILocalizationService _localization;
    private readonly INavigationService _navigation;

    private bool _disposed;

    public GroupDetailsViewModel(
        GetGroupDetailsUseCase getDetails,
        ILocalizationService localization,
        INavigationService navigation)
    {
        _getDetails = getDetails ?? throw new ArgumentNullException(nameof(getDetails));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));

        Students = new ObservableCollection<EnrollmentListItem>();
        Slots = new ObservableCollection<ScheduleSlotItem>();
        DayLabels = new ObservableCollection<string>();
        WeekScheduleItems = new ObservableCollection<WeekScheduleItem>();

        BuildDayLabels();
        _localization.LanguageChanged += OnLanguageChanged;
    }

    public ObservableCollection<EnrollmentListItem> Students { get; }

    public ObservableCollection<ScheduleSlotItem> Slots { get; }

    /// <summary>The seven day headers of the compact timetable (0 = Saturday).</summary>
    public ObservableCollection<string> DayLabels { get; }

    /// <summary>The slots projected for the compact D3 weekly grid.</summary>
    public ObservableCollection<WeekScheduleItem> WeekScheduleItems { get; }

    public string Title => _localization.Tr(TranslationKeys.Groups.Details);

    [ObservableProperty]
    private int _groupId;

    [ObservableProperty]
    private GroupSummary? _group;

    [ObservableProperty]
    private GroupAttendanceStats? _attendance;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasStudents => Students.Count > 0;

    public bool HasSlots => Slots.Count > 0;

    public string GroupName => Group?.Name ?? string.Empty;

    public string SubjectText => Group?.SubjectName ?? _localization.Tr(TranslationKeys.Common.NotSpecified);

    public string TeacherText => Group?.TeacherName ?? _localization.Tr(TranslationKeys.Groups.NoTeacher);

    public string GradeText => Group?.GradeName ?? _localization.Tr(TranslationKeys.Common.NotSpecified);

    public string LevelText => Group?.AcademicLevelName ?? _localization.Tr(TranslationKeys.Common.NotSpecified);

    public string FeeText => Group is null
        ? string.Empty
        : Group.MonthlyFee.ToString("0.##", CultureInfo.InvariantCulture);

    public string CapacityText => Group?.CapacityText ?? string.Empty;

    public bool IsFull => Group?.IsFull ?? false;

    public int ActiveEnrollments => Group?.ActiveEnrollments ?? 0;

    public int MaxStudents => Group?.MaxStudents ?? 0;

    public bool IsActive => Group?.IsActive ?? false;

    public string StatusText => IsActive
        ? _localization.Tr(TranslationKeys.Common.Active)
        : _localization.Tr(TranslationKeys.Common.Inactive);

    public StatusTone StatusTone => IsActive ? StatusTone.Success : StatusTone.Neutral;

    public int CapacityPercent => Group is null || Group.MaxStudents <= 0
        ? 0
        : Math.Clamp((int)Math.Round(Group.ActiveEnrollments * 100d / Group.MaxStudents), 0, 100);

    public string AttendanceText => Attendance is null
        ? string.Empty
        : string.Format(
            CultureInfo.InvariantCulture,
            "{0} / {1} ({2:0.#}%)",
            Attendance.PresentCount + Attendance.LateCount,
            Attendance.RecordCount,
            Attendance.AttendanceRate);

    public async Task InitializeAsync(int groupId, CancellationToken cancellationToken = default)
    {
        GroupId = groupId;
        await LoadAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            var result = await _getDetails.ExecuteAsync(GroupId, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                ShowFailure(result.Failure!);
                return;
            }

            Students.Clear();
            foreach (var student in result.Value.Students)
            {
                Students.Add(student);
            }

            Slots.Clear();
            foreach (var slot in result.Value.Slots.OrderBy(slot => slot.DayOfWeek).ThenBy(slot => slot.StartTime))
            {
                Slots.Add(slot);
            }

            Group = result.Value.Group;
            Attendance = result.Value.Attendance;
            ErrorMessage = string.Empty;
            RaiseProperties();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Back() => _navigation.NavigateTo<GroupsViewModel>();

    [RelayCommand]
    private async Task EditAsync()
    {
        var groups = _navigation.NavigateTo<GroupsViewModel>();
        await groups.BeginEditAsync(GroupId).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task EnrollAsync()
    {
        var page = _navigation.NavigateTo<EnrollmentViewModel>();
        await page.InitializeAsync(GroupId).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task ScheduleAsync()
    {
        var page = _navigation.NavigateTo<GroupScheduleViewModel>();
        await page.InitializeAsync(GroupId).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task ReportAsync()
    {
        var page = _navigation.NavigateTo<GroupFullReportViewModel>();
        await page.InitializeAsync(GroupId).ConfigureAwait(false);
    }

    [RelayCommand]
    private Task ReloadAsync() => LoadAsync();

    /// <summary>Opens the attendance screen for the group's sessions.</summary>
    [RelayCommand]
    private void OpenAttendance() => _navigation.NavigateToTab(ShellTab.Attendance);

    /// <summary>Opens the homework report for the group.</summary>
    [RelayCommand]
    private void OpenHomework() => _navigation.NavigateToTab(ShellTab.Homework);

    private void BuildDayLabels()
    {
        DayLabels.Clear();
        for (var index = 0; index < 7; index++)
        {
            DayLabels.Add(_localization.Tr(
                WeekDayCalculator.ToTranslationKey(WeekDayCalculator.FromIndex(index))));
        }
    }

    /// <summary>Projects the stored slots for the compact D3 grid.</summary>
    private void RebuildGrid()
    {
        WeekScheduleItems.Clear();
        foreach (var slot in Slots)
        {
            var start = slot.StartTime.TimeOfDay;
            var end = slot.EndTime.TimeOfDay;
            var range = string.Format(
                CultureInfo.InvariantCulture,
                "{0} - {1}",
                ScheduleTimeParser.Format(start),
                ScheduleTimeParser.Format(end));

            var subtitle = string.IsNullOrWhiteSpace(slot.Location)
                ? range
                : string.Format(CultureInfo.InvariantCulture, "{0} • {1}", range, slot.Location);

            WeekScheduleItems.Add(new WeekScheduleItem(
                slot.DayOfWeek,
                start,
                end,
                GroupName,
                subtitle,
                slot.IsActive ? StatusTone.Accent : StatusTone.Neutral,
                GroupName,
                slot));
        }
    }

    private void ShowFailure(Failure failure) => ErrorMessage = failure.ToDisplayMessage(_localization);

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnGroupChanged(GroupSummary? value) => RaiseProperties();

    partial void OnAttendanceChanged(GroupAttendanceStats? value)
        => OnPropertyChanged(nameof(AttendanceText));

    private void RaiseProperties()
    {
        OnPropertyChanged(nameof(GroupName));
        OnPropertyChanged(nameof(SubjectText));
        OnPropertyChanged(nameof(TeacherText));
        OnPropertyChanged(nameof(GradeText));
        OnPropertyChanged(nameof(LevelText));
        OnPropertyChanged(nameof(FeeText));
        OnPropertyChanged(nameof(CapacityText));
        OnPropertyChanged(nameof(IsFull));
        OnPropertyChanged(nameof(ActiveEnrollments));
        OnPropertyChanged(nameof(MaxStudents));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusTone));
        OnPropertyChanged(nameof(CapacityPercent));
        OnPropertyChanged(nameof(AttendanceText));
        OnPropertyChanged(nameof(HasStudents));
        OnPropertyChanged(nameof(HasSlots));
        RebuildGrid();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        BuildDayLabels();
        OnPropertyChanged(nameof(Title));
        RaiseProperties();
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
