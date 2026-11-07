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
using SchoolManagement.Domain.Policies;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>One entry of the weekday ComboBox; the label follows the language.</summary>
public sealed record ScheduleDayOption(int Value, string Label);

/// <summary>One weekday block of the group schedule.</summary>
public sealed partial class ScheduleDayGroup : ObservableObject
{
    public ScheduleDayGroup(int dayOfWeek, string label, IEnumerable<ScheduleSlotItem> slots)
    {
        DayOfWeek = dayOfWeek;
        Label = label;
        Slots = new ObservableCollection<ScheduleSlotItem>(slots);
    }

    public int DayOfWeek { get; }

    public string Label { get; }

    public ObservableCollection<ScheduleSlotItem> Slots { get; }

    public bool HasSlots => Slots.Count > 0;
}

/// <summary>One line of the conflict confirmation overlay.</summary>
public sealed record ScheduleConflictLine(string Text);

/// <summary>
/// Weekly schedule of one group. Conflicts are warnings: the administrator can
/// always confirm and save, exactly like the capacity rule of the phone app.
/// </summary>
public sealed partial class GroupScheduleViewModel : PageViewModel, IDisposable
{
    private readonly GetGroupSummaryUseCase _getGroup;
    private readonly GetGroupScheduleUseCase _getSlots;
    private readonly GetScheduleConflictsUseCase _getConflicts;
    private readonly SaveScheduleSlotUseCase _saveSlot;
    private readonly DeleteScheduleSlotUseCase _deleteSlot;
    private readonly ILocalizationService _localization;
    private readonly IUserDialogService _dialogs;
    private readonly INavigationService _navigation;
    private readonly LicenseViewModel _license;

    private ScheduleSaveRequest? _pendingRequest;
    private bool _disposed;

    public GroupScheduleViewModel(
        GetGroupSummaryUseCase getGroup,
        GetGroupScheduleUseCase getSlots,
        GetScheduleConflictsUseCase getConflicts,
        SaveScheduleSlotUseCase saveSlot,
        DeleteScheduleSlotUseCase deleteSlot,
        ILocalizationService localization,
        IUserDialogService dialogs,
        INavigationService navigation,
        LicenseViewModel license)
    {
        _getGroup = getGroup ?? throw new ArgumentNullException(nameof(getGroup));
        _getSlots = getSlots ?? throw new ArgumentNullException(nameof(getSlots));
        _getConflicts = getConflicts ?? throw new ArgumentNullException(nameof(getConflicts));
        _saveSlot = saveSlot ?? throw new ArgumentNullException(nameof(saveSlot));
        _deleteSlot = deleteSlot ?? throw new ArgumentNullException(nameof(deleteSlot));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _license = license ?? throw new ArgumentNullException(nameof(license));

        Days = new ObservableCollection<ScheduleDayGroup>();
        WeekDays = new ObservableCollection<ScheduleDayOption>();
        Conflicts = new ObservableCollection<ScheduleConflictLine>();

        BuildWeekDayOptions();
        _localization.LanguageChanged += OnLanguageChanged;
    }

    public ObservableCollection<ScheduleDayGroup> Days { get; }

    public ObservableCollection<ScheduleDayOption> WeekDays { get; }

    public ObservableCollection<ScheduleConflictLine> Conflicts { get; }

    public string Title => _localization.Tr(TranslationKeys.Groups.ScheduleTitle);

    public string Subtitle => _localization.Tr(TranslationKeys.Groups.ScheduleSubtitle);

    [ObservableProperty]
    private int _groupId;

    [ObservableProperty]
    private GroupSummary? _group;

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

    [ObservableProperty]
    private bool _hasAnySlot;

    [ObservableProperty]
    private bool _isEditorOpen;

    [ObservableProperty]
    private string _editorTitle = string.Empty;

    [ObservableProperty]
    private int? _editingSlotId;

    [ObservableProperty]
    private ScheduleDayOption? _selectedDay;

    [ObservableProperty]
    private string _editorStartTime = "08:00";

    [ObservableProperty]
    private string _editorEndTime = "09:00";

    [ObservableProperty]
    private string _editorLocation = string.Empty;

    [ObservableProperty]
    private bool _editorIsActive = true;

    [ObservableProperty]
    private bool _isConflictOpen;

    [ObservableProperty]
    private string _conflictMessage = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    public bool HasConflicts => Conflicts.Count > 0;

    public string GroupName => Group?.Name ?? string.Empty;

    public string TeacherText => Group?.TeacherName ?? _localization.Tr(TranslationKeys.Groups.NoTeacher);

    public string CapacityText => Group?.CapacityText ?? string.Empty;

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
            var group = await _getGroup.ExecuteAsync(GroupId, cancellationToken).ConfigureAwait(false);
            if (group.IsFailure)
            {
                ShowFailure(group.Failure!);
                return;
            }

            if (group.Value is null)
            {
                ShowFailure(Failure.NotFound(TranslationKeys.Errors.NotFound));
                return;
            }

            var slots = await _getSlots.ExecuteAsync(GroupId, cancellationToken).ConfigureAwait(false);
            if (slots.IsFailure)
            {
                ShowFailure(slots.Failure!);
                return;
            }

            Group = group.Value;
            RebuildDays(slots.Value);
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

    [RelayCommand]
    private void AddSlot()
    {
        ClearMessages();
        EditingSlotId = null;
        SelectedDay = WeekDays.FirstOrDefault();
        EditorStartTime = "08:00";
        EditorEndTime = "09:00";
        EditorLocation = string.Empty;
        EditorIsActive = true;
        EditorTitle = _localization.Tr(TranslationKeys.Groups.AddTimeSlot);
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void EditSlot(ScheduleSlotItem? slot)
    {
        if (slot is null)
        {
            return;
        }

        ClearMessages();
        EditingSlotId = slot.Id;
        SelectedDay = WeekDays.FirstOrDefault(option => option.Value == slot.DayOfWeek);
        EditorStartTime = ScheduleTimeParser.Format(slot.StartTime);
        EditorEndTime = ScheduleTimeParser.Format(slot.EndTime);
        EditorLocation = slot.Location ?? string.Empty;
        EditorIsActive = slot.IsActive;
        EditorTitle = _localization.Tr(TranslationKeys.Groups.EditTimeSlot);
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void CancelEditor() => IsEditorOpen = false;

    [RelayCommand]
    private async Task DeleteSlotAsync(ScheduleSlotItem? slot)
    {
        if (slot is null)
        {
            return;
        }

        var dayLabel = WeekDays.FirstOrDefault(option => option.Value == slot.DayOfWeek)?.Label ?? string.Empty;
        var confirmed = await _dialogs.ConfirmAsync(
            _localization.Tr(TranslationKeys.Groups.ScheduleTitle),
            _localization.Tr(
                TranslationKeys.Groups.DeleteTimeSlotConfirm,
                ("day", dayLabel)),
            _localization.Tr(TranslationKeys.Common.Delete),
            _localization.Tr(TranslationKeys.Common.Cancel)).ConfigureAwait(false);

        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _deleteSlot.ExecuteAsync(slot.Id).ConfigureAwait(false);
            if (result.IsFailure)
            {
                ShowFailure(result.Failure!);
                return;
            }

            ShowSuccess(TranslationKeys.Groups.ScheduleDeleted);
            await LoadAsync().ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveEditorAsync()
    {
        if (!IsEditorOpen || SelectedDay is null)
        {
            return;
        }

        if (!ScheduleTimeParser.TryParse(EditorStartTime, out var start)
            || !ScheduleTimeParser.TryParse(EditorEndTime, out var end))
        {
            ShowFailure(Failure.Validation(TranslationKeys.Groups.InvalidTimeRange));
            return;
        }

        if (!ScheduleConflictPolicy.IsValidRange(start, end))
        {
            ShowFailure(Failure.Validation(TranslationKeys.Groups.InvalidTimeRange));
            return;
        }

        var request = new ScheduleSaveRequest(
            GroupId,
            SelectedDay.Value,
            start,
            end,
            EditorLocation,
            EditorIsActive,
            EditingSlotId);

        IsBusy = true;
        try
        {
            var conflicts = await _getConflicts.ExecuteAsync(
                GroupId,
                request.DayOfWeek,
                request.StartTime,
                request.EndTime,
                request.SlotId).ConfigureAwait(false);

            if (conflicts.IsFailure)
            {
                ShowFailure(conflicts.Failure!);
                return;
            }

            if (conflicts.Value.HasConflicts)
            {
                _pendingRequest = request;
                ShowConflicts(conflicts.Value);
                IsConflictOpen = true;
                return;
            }

            await PersistAsync(request).ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ConfirmConflictAsync()
    {
        var request = _pendingRequest;
        _pendingRequest = null;
        IsConflictOpen = false;
        Conflicts.Clear();

        if (request is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await PersistAsync(request).ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CancelConflict()
    {
        _pendingRequest = null;
        IsConflictOpen = false;
        Conflicts.Clear();
    }

    [RelayCommand]
    private async Task RenewLicenseAsync()
    {
        await _license.RenewInteractiveAsync().ConfigureAwait(false);
        await LoadAsync().ConfigureAwait(false);
    }

    private async Task PersistAsync(ScheduleSaveRequest request)
    {
        var result = await _saveSlot.ExecuteAsync(request).ConfigureAwait(false);
        if (result.IsFailure)
        {
            ShowFailure(result.Failure!);
            return;
        }

        IsEditorOpen = false;
        ShowSuccess(TranslationKeys.Groups.ScheduleSaved);
        await LoadAsync().ConfigureAwait(false);
    }

    private void RebuildDays(IReadOnlyList<ScheduleSlotItem> slots)
    {
        Days.Clear();
        var any = false;
        foreach (var dayOption in WeekDays)
        {
            var daySlots = slots
                .Where(slot => slot.DayOfWeek == dayOption.Value)
                .OrderBy(slot => slot.StartTime)
                .ToList();

            any |= daySlots.Count > 0;
            Days.Add(new ScheduleDayGroup(dayOption.Value, dayOption.Label, daySlots));
        }

        HasAnySlot = any;
    }

    private void ShowConflicts(ScheduleCheckResult check)
    {
        Conflicts.Clear();
        foreach (var conflict in check.Conflicts)
        {
            var kind = conflict.Kind == ScheduleConflictKind.SameGroup
                ? _localization.Tr(TranslationKeys.Groups.ConflictSameGroup)
                : _localization.Tr(TranslationKeys.Groups.ConflictSameTeacher);
            var day = WeekDays.FirstOrDefault(option => option.Value == conflict.DayOfWeek)?.Label
                ?? conflict.DayOfWeek.ToString(CultureInfo.InvariantCulture);
            Conflicts.Add(new ScheduleConflictLine(string.Format(
                CultureInfo.InvariantCulture,
                "• {0} — {1} {2}-{3} ({4})",
                conflict.GroupName ?? string.Empty,
                day,
                ScheduleTimeParser.Format(conflict.Start),
                ScheduleTimeParser.Format(conflict.End),
                kind)));
        }

        ConflictMessage = _localization.Tr(TranslationKeys.Groups.ConflictMessage);
    }

    private void BuildWeekDayOptions()
    {
        WeekDays.Clear();
        for (var index = 0; index < 7; index++)
        {
            var day = WeekDayCalculator.FromIndex(index);
            WeekDays.Add(new ScheduleDayOption(
                index,
                _localization.Tr(WeekDayCalculator.ToTranslationKey(day))));
        }
    }

    private void ShowFailure(Failure failure)
    {
        ErrorMessage = failure.ToDisplayMessage(_localization);
        StatusMessage = string.Empty;
        IsRenewVisible = failure.Kind == FailureKind.License;
    }

    private void ShowSuccess(string messageKey)
    {
        ErrorMessage = string.Empty;
        StatusMessage = _localization.Tr(messageKey);
        IsRenewVisible = false;
    }

    private void ClearMessages()
    {
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        IsRenewVisible = false;
    }

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    partial void OnGroupChanged(GroupSummary? value)
    {
        OnPropertyChanged(nameof(GroupName));
        OnPropertyChanged(nameof(TeacherText));
        OnPropertyChanged(nameof(CapacityText));
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        BuildWeekDayOptions();
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Subtitle));
        EditorTitle = _localization.Tr(
            EditingSlotId is null ? TranslationKeys.Groups.AddTimeSlot : TranslationKeys.Groups.EditTimeSlot);
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
