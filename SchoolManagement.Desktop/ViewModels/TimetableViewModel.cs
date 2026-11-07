using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Groups;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.UseCases;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>One filter entry of the timetable (a null id means "all").</summary>
public sealed record TimetableFilterOption(int? Id, string Label);

/// <summary>One cell of the visual timetable grid.</summary>
public sealed record TimetableCell(string Text, bool IsHeader, bool IsDayLabel, bool HasSlot);

/// <summary>
/// Visual weekly timetable: seven rows (Saturday first) and one column per hour
/// from 08:00 to 19:00. The grid is built entirely by the ViewModel, so the view
/// only renders a flat <c>ItemsControl</c> over a <c>UniformGrid</c>.
/// </summary>
public sealed partial class TimetableViewModel : PageViewModel, IDisposable
{
    private const int FirstHour = 8;
    private const int LastHour = 19;
    private const int HourColumns = LastHour - FirstHour + 1;

    private readonly GetTimetableUseCase _getTimetable;
    private readonly GetGroupsUseCase _getGroups;
    private readonly GetGroupLookupsUseCase _getLookups;
    private readonly ILocalizationService _localization;
    private readonly INavigationService _navigation;

    private IReadOnlyList<ScheduleSlotItem> _slots = Array.Empty<ScheduleSlotItem>();
    private bool _isLoaded;
    private bool _suppressReload;
    private bool _disposed;

    public TimetableViewModel(
        GetTimetableUseCase getTimetable,
        GetGroupsUseCase getGroups,
        GetGroupLookupsUseCase getLookups,
        ILocalizationService localization,
        INavigationService navigation)
    {
        _getTimetable = getTimetable ?? throw new ArgumentNullException(nameof(getTimetable));
        _getGroups = getGroups ?? throw new ArgumentNullException(nameof(getGroups));
        _getLookups = getLookups ?? throw new ArgumentNullException(nameof(getLookups));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));

        Cells = new ObservableCollection<TimetableCell>();
        GroupOptions = new ObservableCollection<TimetableFilterOption>();
        TeacherOptions = new ObservableCollection<TimetableFilterOption>();

        _localization.LanguageChanged += OnLanguageChanged;
    }

    public ObservableCollection<TimetableCell> Cells { get; }

    public ObservableCollection<TimetableFilterOption> GroupOptions { get; }

    public ObservableCollection<TimetableFilterOption> TeacherOptions { get; }

    public string Title => _localization.Tr(TranslationKeys.Timetable.Title);

    /// <summary>One label column plus one column per hour.</summary>
    public int ColumnCount => HourColumns + 1;

    /// <summary>One header row plus seven school days.</summary>
    public int RowCount => 8;

    [ObservableProperty]
    private TimetableFilterOption? _selectedGroup;

    [ObservableProperty]
    private TimetableFilterOption? _selectedTeacher;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isEmpty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public async Task InitializeAsync(
        int? groupId,
        int? teacherId,
        CancellationToken cancellationToken = default)
    {
        await LoadLookupsAsync(cancellationToken).ConfigureAwait(false);

        _suppressReload = true;
        try
        {
            SelectedGroup = GroupOptions.FirstOrDefault(option => option.Id == groupId)
                ?? GroupOptions.FirstOrDefault();
            SelectedTeacher = TeacherOptions.FirstOrDefault(option => option.Id == teacherId)
                ?? TeacherOptions.FirstOrDefault();
        }
        finally
        {
            _suppressReload = false;
        }

        await LoadAsync(cancellationToken).ConfigureAwait(false);
        _isLoaded = true;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            var result = await _getTimetable
                .ExecuteAsync(SelectedGroup?.Id, SelectedTeacher?.Id, cancellationToken)
                .ConfigureAwait(false);

            if (result.IsFailure)
            {
                ShowFailure(result.Failure!);
                return;
            }

            _slots = result.Value;
            BuildGrid(_slots);
            ErrorMessage = string.Empty;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Back() => _navigation.NavigateTo<GroupsViewModel>();

    [RelayCommand]
    private Task ReloadAsync() => LoadAsync();

    private void BuildGrid(IReadOnlyList<ScheduleSlotItem> slots)
    {
        Cells.Clear();
        IsEmpty = slots.Count == 0;

        Cells.Add(new TimetableCell(string.Empty, IsHeader: true, IsDayLabel: true, HasSlot: false));
        for (var hour = FirstHour; hour <= LastHour; hour++)
        {
            Cells.Add(new TimetableCell(
                string.Format(CultureInfo.InvariantCulture, "{0:00}:00", hour),
                IsHeader: true,
                IsDayLabel: false,
                HasSlot: false));
        }

        for (var dayIndex = 0; dayIndex < 7; dayIndex++)
        {
            var day = WeekDayCalculator.FromIndex(dayIndex);
            Cells.Add(new TimetableCell(
                _localization.Tr(WeekDayCalculator.ToTranslationKey(day)),
                IsHeader: false,
                IsDayLabel: true,
                HasSlot: false));

            for (var hour = FirstHour; hour <= LastHour; hour++)
            {
                var matching = slots
                    .Where(slot => slot.DayOfWeek == dayIndex && slot.StartTime.Hour == hour)
                    .OrderBy(slot => slot.StartTime)
                    .ToList();

                Cells.Add(new TimetableCell(
                    BuildCellText(matching),
                    IsHeader: false,
                    IsDayLabel: false,
                    HasSlot: matching.Count > 0));
            }
        }
    }

    private static string BuildCellText(IReadOnlyList<ScheduleSlotItem> slots)
    {
        if (slots.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            "\n",
            slots.Select(slot => string.Format(
                CultureInfo.InvariantCulture,
                "{0}\n{1:HH:mm}-{2:HH:mm}{3}",
                slot.GroupName,
                slot.StartTime,
                slot.EndTime,
                string.IsNullOrWhiteSpace(slot.Location) ? string.Empty : $" · {slot.Location}")));
    }

    private async Task LoadLookupsAsync(CancellationToken cancellationToken)
    {
        var groups = await _getGroups
            .ExecuteAsync(new GroupListQuery(), cancellationToken)
            .ConfigureAwait(false);
        var lookups = await _getLookups.ExecuteAsync(cancellationToken).ConfigureAwait(false);

        if (groups.IsFailure)
        {
            ShowFailure(groups.Failure!);
            return;
        }

        if (lookups.IsFailure)
        {
            ShowFailure(lookups.Failure!);
            return;
        }

        _suppressReload = true;
        try
        {
            GroupOptions.Clear();
            GroupOptions.Add(new TimetableFilterOption(null, _localization.Tr(TranslationKeys.Timetable.AllGroups)));
            foreach (var group in groups.Value.OrderBy(group => group.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                GroupOptions.Add(new TimetableFilterOption(group.Id, group.Name));
            }

            TeacherOptions.Clear();
            TeacherOptions.Add(new TimetableFilterOption(null, _localization.Tr(TranslationKeys.Timetable.AllTeachers)));
            foreach (var teacher in lookups.Value.Teachers)
            {
                TeacherOptions.Add(new TimetableFilterOption(teacher.Id, teacher.Name));
            }
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

        await LoadAsync().ConfigureAwait(false);
    }

    partial void OnSelectedGroupChanged(TimetableFilterOption? value) => _ = RefreshIfLoadedAsync();

    partial void OnSelectedTeacherChanged(TimetableFilterOption? value) => _ = RefreshIfLoadedAsync();

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    private void ShowFailure(Failure failure) => ErrorMessage = failure.ToDisplayMessage(_localization);

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Title));

        _suppressReload = true;
        try
        {
            if (GroupOptions.Count > 0)
            {
                GroupOptions[0] = new TimetableFilterOption(
                    null,
                    _localization.Tr(TranslationKeys.Timetable.AllGroups));
            }

            if (TeacherOptions.Count > 0)
            {
                TeacherOptions[0] = new TimetableFilterOption(
                    null,
                    _localization.Tr(TranslationKeys.Timetable.AllTeachers));
            }
        }
        finally
        {
            _suppressReload = false;
        }

        BuildGrid(_slots);
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
