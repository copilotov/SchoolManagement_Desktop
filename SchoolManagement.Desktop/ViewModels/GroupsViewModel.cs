using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Groups;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Time;
using SchoolManagement.Application.UseCases;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>One option of the group sort ComboBox; the label follows the language.</summary>
public sealed record GroupSortOption(GroupSortField Field, string Label);

/// <summary>One lookup option (grade, teacher, level) of the group form.</summary>
public sealed record GroupLookupOption(int Id, string Label);

/// <summary>
/// Group list screen: instant search/sort, capacity and schedule badges, the
/// shared add/edit form and the navigation shortcuts to enrollment, schedule,
/// timetable, details and the printable report. Every operation runs through a
/// use case, so the write gate is always applied.
/// </summary>
public sealed partial class GroupsViewModel : PageViewModel, IDisposable
{
    private readonly GetGroupsUseCase _getGroups;
    private readonly GetGroupLookupsUseCase _getLookups;
    private readonly GetGroupSubjectsUseCase _getSubjects;
    private readonly GetGroupSummaryUseCase _getSummary;
    private readonly SaveGroupUseCase _saveGroup;
    private readonly RemoveGroupUseCase _removeGroup;
    private readonly GenerateSessionsUseCase _generateSessions;
    private readonly ILocalizationService _localization;
    private readonly IUserDialogService _dialogs;
    private readonly INavigationService _navigation;
    private readonly ITrustedClock _clock;
    private readonly LicenseViewModel _license;

    private bool _suppressReload;
    private bool _suppressCascade;
    private bool _isLoaded;
    private bool _disposed;

    public GroupsViewModel(
        GetGroupsUseCase getGroups,
        GetGroupLookupsUseCase getLookups,
        GetGroupSubjectsUseCase getSubjects,
        GetGroupSummaryUseCase getSummary,
        SaveGroupUseCase saveGroup,
        RemoveGroupUseCase removeGroup,
        GenerateSessionsUseCase generateSessions,
        ILocalizationService localization,
        IUserDialogService dialogs,
        INavigationService navigation,
        ITrustedClock clock,
        LicenseViewModel license)
    {
        _getGroups = getGroups ?? throw new ArgumentNullException(nameof(getGroups));
        _getLookups = getLookups ?? throw new ArgumentNullException(nameof(getLookups));
        _getSubjects = getSubjects ?? throw new ArgumentNullException(nameof(getSubjects));
        _getSummary = getSummary ?? throw new ArgumentNullException(nameof(getSummary));
        _saveGroup = saveGroup ?? throw new ArgumentNullException(nameof(saveGroup));
        _removeGroup = removeGroup ?? throw new ArgumentNullException(nameof(removeGroup));
        _generateSessions = generateSessions ?? throw new ArgumentNullException(nameof(generateSessions));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _license = license ?? throw new ArgumentNullException(nameof(license));

        Items = new ObservableCollection<GroupSummary>();
        SortOptions = new ObservableCollection<GroupSortOption>();
        Grades = new ObservableCollection<GroupLookupOption>();
        Subjects = new ObservableCollection<GroupSubjectOption>();
        Teachers = new ObservableCollection<GroupLookupOption>();

        RebuildStaticOptions();
        _localization.LanguageChanged += OnLanguageChanged;
    }

    public ObservableCollection<GroupSummary> Items { get; }

    public ObservableCollection<GroupSortOption> SortOptions { get; }

    public ObservableCollection<GroupLookupOption> Grades { get; }

    public ObservableCollection<GroupSubjectOption> Subjects { get; }

    public ObservableCollection<GroupLookupOption> Teachers { get; }

    public LicenseViewModel License => _license;

    [ObservableProperty]
    private UiState<IReadOnlyList<GroupSummary>> _state = new UiState<IReadOnlyList<GroupSummary>>.Initial();

    [ObservableProperty]
    private string? _searchText;

    [ObservableProperty]
    private bool _includeInactive;

    [ObservableProperty]
    private GroupSortOption? _selectedSort;

    [ObservableProperty]
    private bool _sortDescending;

    [ObservableProperty]
    private GroupSummary? _selectedItem;

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
    private string _editorName = string.Empty;

    [ObservableProperty]
    private GroupLookupOption? _selectedEditorGrade;

    [ObservableProperty]
    private GroupSubjectOption? _selectedEditorSubject;

    [ObservableProperty]
    private GroupLookupOption? _selectedEditorTeacher;

    [ObservableProperty]
    private string _editorMaxStudents = "30";

    [ObservableProperty]
    private string _editorMonthlyFee = "0";

    [ObservableProperty]
    private bool _editorIsActive = true;

    [ObservableProperty]
    private bool _isSubjectsLoading;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    public bool IsEmpty => State is UiState<IReadOnlyList<GroupSummary>>.Empty;

    public bool IsLoading => State.IsLoading;

    /// <summary>Builds the deliberate query of the group table.</summary>
    public GroupListQuery CreateQuery() => new(
        SearchText,
        IncludeInactive,
        SelectedSort?.Field ?? GroupSortField.Name,
        SortDescending);

    /// <summary>Loads the lookup lists once, then the first page.</summary>
    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_isLoaded)
        {
            return;
        }

        await LoadLookupsAsync(cancellationToken).ConfigureAwait(false);
        await RefreshAsync(cancellationToken).ConfigureAwait(false);
        _isLoaded = true;
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        State = new UiState<IReadOnlyList<GroupSummary>>.Loading();
        var result = await _getGroups.ExecuteAsync(CreateQuery(), cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            Items.Clear();
            State = new UiState<IReadOnlyList<GroupSummary>>.Error(result.Failure!);
            ShowFailure(result.Failure!);
            return;
        }

        Items.Clear();
        foreach (var item in result.Value)
        {
            Items.Add(item);
        }

        State = result.Value.Count == 0
            ? new UiState<IReadOnlyList<GroupSummary>>.Empty()
            : new UiState<IReadOnlyList<GroupSummary>>.Success(result.Value);

        // A reload keeps the last status banner so a save/delete/generate
        // message survives the refresh, exactly like Phases 4 and 5.
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>Opens the shared form for a group, used by the details page.</summary>
    public Task BeginEditAsync(int groupId) => OpenEditorAsync(groupId);

    [RelayCommand]
    private Task SearchAsync() => RefreshAsync();

    [RelayCommand]
    private Task ReloadAsync() => RefreshAsync();

    [RelayCommand]
    private async Task AddAsync()
    {
        ClearMessages();
        ResetEditor();
        EditorTitle = _localization.Tr(TranslationKeys.Groups.Add);
        SelectedEditorGrade = Grades.FirstOrDefault();
        EditorIsActive = true;
        IsEditorOpen = true;
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task EditAsync(GroupSummary? item)
    {
        var target = item ?? SelectedItem;
        if (target is null)
        {
            return;
        }

        await OpenEditorAsync(target.Id).ConfigureAwait(false);
    }

    [RelayCommand]
    private Task EditSelectedAsync() => EditAsync(SelectedItem);

    [RelayCommand]
    private async Task DeleteAsync(GroupSummary? item)
    {
        var target = item ?? SelectedItem;
        if (target is null)
        {
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            _localization.Tr(TranslationKeys.Groups.DeleteTitle),
            _localization.Tr(
                TranslationKeys.Groups.DeleteMessage,
                ("name", target.Name)),
            _localization.Tr(TranslationKeys.Common.Delete),
            _localization.Tr(TranslationKeys.Common.Cancel)).ConfigureAwait(false);

        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _removeGroup.ExecuteAsync(target.Id).ConfigureAwait(false);
            if (result.IsFailure)
            {
                ShowFailure(result.Failure!);
                return;
            }

            ShowSuccess(TranslationKeys.Groups.Deleted);
            await RefreshAsync().ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task DeleteSelectedAsync() => DeleteAsync(SelectedItem);

    [RelayCommand]
    private async Task DetailsAsync(GroupSummary? item)
    {
        var target = item ?? SelectedItem;
        if (target is null)
        {
            return;
        }

        var page = _navigation.NavigateTo<GroupDetailsViewModel>();
        await page.InitializeAsync(target.Id).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task EnrollAsync(GroupSummary? item)
    {
        var target = item ?? SelectedItem;
        if (target is null)
        {
            return;
        }

        var page = _navigation.NavigateTo<EnrollmentViewModel>();
        await page.InitializeAsync(target.Id).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task ScheduleAsync(GroupSummary? item)
    {
        var target = item ?? SelectedItem;
        if (target is null)
        {
            return;
        }

        var page = _navigation.NavigateTo<GroupScheduleViewModel>();
        await page.InitializeAsync(target.Id).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task GenerateAsync(GroupSummary? item)
    {
        var target = item ?? SelectedItem;
        if (target is null)
        {
            return;
        }

        await GenerateForAsync(target.Id).ConfigureAwait(false);
    }

    [RelayCommand]
    private Task GenerateAllAsync() => GenerateForAsync(null);

    [RelayCommand]
    private async Task OpenTimetableAsync()
    {
        var page = _navigation.NavigateTo<TimetableViewModel>();
        await page.InitializeAsync(null, null).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task SaveEditorAsync()
    {
        if (!IsEditorOpen)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _saveGroup.ExecuteAsync(CreateSaveRequest()).ConfigureAwait(false);
            if (result.IsFailure)
            {
                ShowFailure(result.Failure!);
                return;
            }

            IsEditorOpen = false;
            ShowSuccess(TranslationKeys.Groups.Saved);
            await RefreshAsync().ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CancelEditor()
    {
        IsEditorOpen = false;
        EditorTitle = string.Empty;
    }

    [RelayCommand]
    private async Task RenewLicenseAsync()
    {
        await _license.RenewInteractiveAsync().ConfigureAwait(false);
        await RefreshAsync().ConfigureAwait(false);
    }

    /// <summary>Collects the form into the request consumed by the save use case.</summary>
    public GroupSaveRequest CreateSaveRequest()
    {
        var maxStudents = int.TryParse(
            EditorMaxStudents,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsedMax)
            ? parsedMax
            : 0;

        var fee = double.TryParse(
            EditorMonthlyFee,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsedFee)
            ? parsedFee
            : 0;

        return new GroupSaveRequest(
            EditorName,
            AcademicLevelId: null,
            SelectedEditorGrade?.Id,
            SelectedEditorSubject?.Id,
            SelectedEditorTeacher?.Id,
            maxStudents,
            fee,
            EditorIsActive,
            GroupId: EditingId);
    }

    partial void OnSearchTextChanged(string? value) => _ = RefreshIfLoadedAsync();

    partial void OnIncludeInactiveChanged(bool value) => _ = RefreshIfLoadedAsync();

    partial void OnSelectedSortChanged(GroupSortOption? value) => _ = RefreshIfLoadedAsync();

    partial void OnSortDescendingChanged(bool value) => _ = RefreshIfLoadedAsync();

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    partial void OnSelectedEditorGradeChanged(GroupLookupOption? value)
        => _ = LoadSubjectsForGradeAsync(value?.Id);

    private async Task RefreshIfLoadedAsync()
    {
        if (_suppressReload || !_isLoaded)
        {
            return;
        }

        await RefreshAsync().ConfigureAwait(false);
    }

    private async Task LoadLookupsAsync(CancellationToken cancellationToken)
    {
        var result = await _getLookups.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            ShowFailure(result.Failure!);
            return;
        }

        _suppressReload = true;
        _suppressCascade = true;
        try
        {
            var previousGrade = SelectedEditorGrade?.Id;
            var previousTeacher = SelectedEditorTeacher?.Id;

            Grades.Clear();
            foreach (var grade in result.Value.Grades)
            {
                Grades.Add(new GroupLookupOption(grade.Id, grade.Name));
            }

            Teachers.Clear();
            Teachers.Add(new GroupLookupOption(0, _localization.Tr(TranslationKeys.Groups.NoTeacher)));
            foreach (var teacher in result.Value.Teachers)
            {
                Teachers.Add(new GroupLookupOption(teacher.Id, teacher.Name));
            }

            SelectedEditorGrade = Grades.FirstOrDefault(option => option.Id == previousGrade);
            SelectedEditorTeacher = Teachers.FirstOrDefault(option => option.Id == previousTeacher);
        }
        finally
        {
            _suppressCascade = false;
            _suppressReload = false;
        }
    }

    private async Task LoadSubjectsForGradeAsync(int? gradeId)
    {
        if (_suppressCascade)
        {
            return;
        }

        var previousSubject = SelectedEditorSubject?.Id;
        Subjects.Clear();
        SelectedEditorSubject = null;

        if (gradeId is null || gradeId <= 0)
        {
            return;
        }

        var id = gradeId.Value;

        IsSubjectsLoading = true;
        try
        {
            var result = await _getSubjects.ExecuteAsync(id).ConfigureAwait(false);
            if (result.IsFailure)
            {
                ShowFailure(result.Failure!);
                return;
            }

            foreach (var subject in result.Value)
            {
                Subjects.Add(subject);
            }

            SelectedEditorSubject = Subjects.FirstOrDefault(option => option.Id == previousSubject);
        }
        finally
        {
            IsSubjectsLoading = false;
        }
    }

    private async Task OpenEditorAsync(int groupId)
    {
        IsBusy = true;
        try
        {
            var result = await _getSummary.ExecuteAsync(groupId).ConfigureAwait(false);
            if (result.IsFailure)
            {
                ShowFailure(result.Failure!);
                return;
            }

            if (result.Value is null)
            {
                ShowFailure(Failure.NotFound(TranslationKeys.Errors.NotFound));
                return;
            }

            var group = result.Value;
            ClearMessages();

            _suppressCascade = true;
            try
            {
                EditingId = group.Id;
                EditorName = group.Name;
                SelectedEditorGrade = Grades.FirstOrDefault(option => option.Id == group.GradeId);
                SelectedEditorTeacher = Teachers.FirstOrDefault(option => option.Id == group.TeacherId)
                    ?? Teachers.FirstOrDefault();
                EditorMaxStudents = group.MaxStudents.ToString(CultureInfo.InvariantCulture);
                EditorMonthlyFee = group.MonthlyFee.ToString("0.##", CultureInfo.InvariantCulture);
                EditorIsActive = group.IsActive;
            }
            finally
            {
                _suppressCascade = false;
            }

            await LoadSubjectsForGradeAsync(group.GradeId).ConfigureAwait(false);
            SelectedEditorSubject = Subjects.FirstOrDefault(option => option.Id == group.SubjectId);
            EditorTitle = _localization.Tr(TranslationKeys.Groups.Edit);
            IsEditorOpen = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task GenerateForAsync(int? groupId)
    {
        var now = _clock.LocalNow;
        var date = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);

        IsBusy = true;
        try
        {
            var result = await _generateSessions
                .ExecuteAsync(new SessionGenerationRequest(date, groupId))
                .ConfigureAwait(false);

            if (result.IsFailure)
            {
                ShowFailure(result.Failure!);
                return;
            }

            if (result.Value.CreatedCount > 0)
            {
                ShowSuccess(TranslationKeys.Groups.GenerationCreated, ("count", result.Value.CreatedCount.ToString(CultureInfo.InvariantCulture)));
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

    private void ResetEditor()
    {
        EditingId = null;
        EditorName = string.Empty;
        SelectedEditorGrade = null;
        SelectedEditorSubject = null;
        SelectedEditorTeacher = null;
        EditorMaxStudents = "30";
        EditorMonthlyFee = "0";
        EditorIsActive = true;
        Subjects.Clear();
    }

    private void ShowFailure(Failure failure)
    {
        ErrorMessage = failure.ToDisplayMessage(_localization);
        StatusMessage = string.Empty;
        IsRenewVisible = failure.Kind == FailureKind.License;
    }

    private void ShowSuccess(string messageKey, params (string Key, object Value)[] parameters)
    {
        ErrorMessage = string.Empty;
        StatusMessage = parameters.Length == 0
            ? _localization.Tr(messageKey)
            : _localization.Tr(
                messageKey,
                parameters.ToDictionary(
                    parameter => parameter.Key,
                    parameter => Convert.ToString(parameter.Value, CultureInfo.InvariantCulture) ?? string.Empty,
                    StringComparer.Ordinal));
        IsRenewVisible = false;
    }

    private void ClearMessages()
    {
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        IsRenewVisible = false;
    }

    private void RebuildStaticOptions()
    {
        _suppressReload = true;
        try
        {
            var previousSort = SelectedSort?.Field ?? GroupSortField.Name;
            SortOptions.Clear();
            SortOptions.Add(new GroupSortOption(GroupSortField.Name, _localization.Tr(TranslationKeys.Groups.SortName)));
            SortOptions.Add(new GroupSortOption(GroupSortField.Subject, _localization.Tr(TranslationKeys.Groups.SortSubject)));
            SortOptions.Add(new GroupSortOption(GroupSortField.Teacher, _localization.Tr(TranslationKeys.Groups.SortTeacher)));
            SortOptions.Add(new GroupSortOption(GroupSortField.Grade, _localization.Tr(TranslationKeys.Groups.SortGrade)));
            SortOptions.Add(new GroupSortOption(GroupSortField.Capacity, _localization.Tr(TranslationKeys.Groups.SortCapacity)));
            SortOptions.Add(new GroupSortOption(GroupSortField.Fee, _localization.Tr(TranslationKeys.Groups.SortFee)));
            SelectedSort = SortOptions.First(option => option.Field == previousSort);

            if (Teachers.Count > 0)
            {
                Teachers[0] = new GroupLookupOption(0, _localization.Tr(TranslationKeys.Groups.NoTeacher));
            }
        }
        finally
        {
            _suppressReload = false;
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RebuildStaticOptions();
        if (IsEditorOpen)
        {
            EditorTitle = _localization.Tr(
                EditingId is null ? TranslationKeys.Groups.Add : TranslationKeys.Groups.Edit);
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
