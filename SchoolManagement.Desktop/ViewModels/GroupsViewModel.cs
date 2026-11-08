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
using SchoolManagement.Desktop.Controls;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>One option of the group sort ComboBox; the label follows the language.</summary>
public sealed record GroupSortOption(GroupSortField Field, string Label);

/// <summary>One lookup option (grade, teacher, level) of the group form.</summary>
public sealed record GroupLookupOption(int Id, string Label);

/// <summary>Table or card presentation of the group list.</summary>
public enum GroupViewMode
{
    Table,
    Cards,
}

/// <summary>How the card list is sectioned.</summary>
public enum GroupGrouping
{
    None,
    Level,
    Teacher,
}

/// <summary>One entry of the "group by" ComboBox; the label follows the language.</summary>
public sealed record GroupGroupingOption(GroupGrouping Value, string Label);

/// <summary>Display-only projection of a group row for the card list.</summary>
public sealed record GroupCardItem(
    GroupSummary Item,
    string Name,
    string Placement,
    string SubjectText,
    string TeacherText,
    string CapacityText,
    int CapacityPercent,
    string SlotsBadge,
    bool HasSchedule,
    bool IsFull,
    bool IsActive,
    string StatusText,
    StatusTone StatusTone,
    StatusTone SlotsTone);

/// <summary>One section of the card list (all groups, or one level/teacher).</summary>
public sealed record GroupSection(string Title, IReadOnlyList<GroupCardItem> Items)
{
    public bool HasTitle => !string.IsNullOrWhiteSpace(Title);
}

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
        Cards = new ObservableCollection<GroupCardItem>();
        Sections = new ObservableCollection<GroupSection>();
        SortOptions = new ObservableCollection<GroupSortOption>();
        GroupingOptions = new ObservableCollection<GroupGroupingOption>();
        Grades = new ObservableCollection<GroupLookupOption>();
        Subjects = new ObservableCollection<GroupSubjectOption>();
        Teachers = new ObservableCollection<GroupLookupOption>();

        RebuildStaticOptions();
        _localization.LanguageChanged += OnLanguageChanged;
    }

    public ObservableCollection<GroupSummary> Items { get; }

    /// <summary>The same rows, projected for the card list.</summary>
    public ObservableCollection<GroupCardItem> Cards { get; }

    /// <summary>One section per level/teacher, or a single untitled section.</summary>
    public ObservableCollection<GroupSection> Sections { get; }

    public ObservableCollection<GroupSortOption> SortOptions { get; }

    public ObservableCollection<GroupGroupingOption> GroupingOptions { get; }

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

    [ObservableProperty]
    private GroupViewMode _viewMode = GroupViewMode.Table;

    [ObservableProperty]
    private GroupGroupingOption? _selectedGrouping;

    [ObservableProperty]
    private string _nameError = string.Empty;

    [ObservableProperty]
    private string _gradeError = string.Empty;

    [ObservableProperty]
    private string _subjectError = string.Empty;

    [ObservableProperty]
    private string _capacityError = string.Empty;

    [ObservableProperty]
    private string _feeError = string.Empty;

    public bool IsTableView => ViewMode == GroupViewMode.Table;

    public bool IsCardView => ViewMode == GroupViewMode.Cards;

    public bool HasCards => Cards.Count > 0;

    public bool HasNoGrades => Grades.Count == 0;

    public bool HasNameError => !string.IsNullOrEmpty(NameError);

    public bool HasGradeError => !string.IsNullOrEmpty(GradeError);

    public bool HasSubjectError => !string.IsNullOrEmpty(SubjectError);

    public bool HasCapacityError => !string.IsNullOrEmpty(CapacityError);

    public bool HasFeeError => !string.IsNullOrEmpty(FeeError);

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
            Cards.Clear();
            Sections.Clear();
            State = new UiState<IReadOnlyList<GroupSummary>>.Error(result.Failure!);
            ShowFailure(result.Failure!);
            return;
        }

        Items.Clear();
        foreach (var item in result.Value)
        {
            Items.Add(item);
        }

        RebuildCards();

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
    private void ShowTable() => ViewMode = GroupViewMode.Table;

    [RelayCommand]
    private void ShowCards() => ViewMode = GroupViewMode.Cards;

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

        if (!ValidateEditor())
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

    partial void OnViewModeChanged(GroupViewMode value)
    {
        OnPropertyChanged(nameof(IsTableView));
        OnPropertyChanged(nameof(IsCardView));
    }

    partial void OnSelectedGroupingChanged(GroupGroupingOption? value) => RebuildSections();

    partial void OnNameErrorChanged(string value) => OnPropertyChanged(nameof(HasNameError));

    partial void OnGradeErrorChanged(string value) => OnPropertyChanged(nameof(HasGradeError));

    partial void OnSubjectErrorChanged(string value) => OnPropertyChanged(nameof(HasSubjectError));

    partial void OnCapacityErrorChanged(string value) => OnPropertyChanged(nameof(HasCapacityError));

    partial void OnFeeErrorChanged(string value) => OnPropertyChanged(nameof(HasFeeError));

    partial void OnEditorNameChanged(string value) => NameError = string.Empty;

    partial void OnEditorMaxStudentsChanged(string value) => CapacityError = string.Empty;

    partial void OnEditorMonthlyFeeChanged(string value) => FeeError = string.Empty;

    partial void OnSelectedEditorSubjectChanged(GroupSubjectOption? value) => SubjectError = string.Empty;

    partial void OnSelectedEditorGradeChanged(GroupLookupOption? value)
    {
        GradeError = string.Empty;
        _ = LoadSubjectsForGradeAsync(value?.Id);
    }

    /// <summary>
    /// Validates the form locally so an obvious mistake never reaches the use
    /// case. The use case keeps its own rules; this only short-circuits the
    /// round-trip and puts the message under the right field.
    /// </summary>
    public bool ValidateEditor()
    {
        NameError = string.IsNullOrWhiteSpace(EditorName)
            ? _localization.Tr(TranslationKeys.Groups.NameRequired)
            : string.Empty;

        GradeError = SelectedEditorGrade is null or { Id: <= 0 }
            ? _localization.Tr(TranslationKeys.Groups.GradeRequired)
            : string.Empty;

        SubjectError = SelectedEditorSubject is null or { Id: <= 0 }
            ? _localization.Tr(TranslationKeys.Groups.SubjectRequired)
            : string.Empty;

        var capacityValid = int.TryParse(
            EditorMaxStudents,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var capacity);
        CapacityError = !capacityValid || capacity < 1 || capacity > 500
            ? _localization.Tr(TranslationKeys.Groups.CapacityInvalid)
            : string.Empty;

        var feeValid = double.TryParse(
            EditorMonthlyFee,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var fee);
        FeeError = !feeValid || fee < 0
            ? _localization.Tr(TranslationKeys.Groups.FeeInvalid)
            : string.Empty;

        return !HasNameError && !HasGradeError && !HasSubjectError && !HasCapacityError && !HasFeeError;
    }

    private void RebuildCards()
    {
        Cards.Clear();
        foreach (var item in Items)
        {
            Cards.Add(CreateCard(item));
        }

        RebuildSections();
        OnPropertyChanged(nameof(HasCards));
    }

    private void RebuildSections()
    {
        Sections.Clear();

        var grouping = SelectedGrouping?.Value ?? GroupGrouping.None;
        if (grouping == GroupGrouping.None)
        {
            Sections.Add(new GroupSection(string.Empty, Cards.ToList()));
            return;
        }

        var sections = Cards
            .GroupBy(card => grouping == GroupGrouping.Level
                ? card.Item.AcademicLevelName ?? _localization.Tr(TranslationKeys.Common.NotSpecified)
                : card.Item.TeacherName ?? _localization.Tr(TranslationKeys.Groups.NoTeacherSection))
            .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase);

        foreach (var group in sections)
        {
            Sections.Add(new GroupSection(group.Key, group.ToList()));
        }
    }

    private GroupCardItem CreateCard(GroupSummary item)
    {
        var percent = item.MaxStudents <= 0
            ? 0
            : Math.Clamp((int)Math.Round(item.ActiveEnrollments * 100d / item.MaxStudents), 0, 100);

        return new GroupCardItem(
            item,
            item.Name,
            PlacementText(item),
            item.SubjectName ?? _localization.Tr(TranslationKeys.Common.NotSpecified),
            item.TeacherName ?? _localization.Tr(TranslationKeys.Groups.NoTeacher),
            item.CapacityText,
            percent,
            item.HasSchedule
                ? _localization.Tr(
                    TranslationKeys.Groups.SlotsBadge,
                    ("count", item.WeeklySlots.ToString(CultureInfo.InvariantCulture)))
                : _localization.Tr(TranslationKeys.Groups.NoSlotsShort),
            item.HasSchedule,
            item.IsFull,
            item.IsActive,
            item.IsActive
                ? _localization.Tr(TranslationKeys.Common.Active)
                : _localization.Tr(TranslationKeys.Common.Inactive),
            item.IsActive ? StatusTone.Success : StatusTone.Neutral,
            item.HasSchedule ? StatusTone.Info : StatusTone.Neutral);
    }

    private string PlacementText(GroupSummary item)
    {
        var parts = new[] { item.AcademicLevelName, item.GradeName }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        return parts.Length == 0
            ? _localization.Tr(TranslationKeys.Common.NotSpecified)
            : string.Join(" • ", parts);
    }

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
            OnPropertyChanged(nameof(HasNoGrades));
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
        NameError = string.Empty;
        GradeError = string.Empty;
        SubjectError = string.Empty;
        CapacityError = string.Empty;
        FeeError = string.Empty;
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

            var previousGrouping = SelectedGrouping?.Value ?? GroupGrouping.None;
            GroupingOptions.Clear();
            GroupingOptions.Add(new GroupGroupingOption(
                GroupGrouping.None, _localization.Tr(TranslationKeys.Groups.GroupByNone)));
            GroupingOptions.Add(new GroupGroupingOption(
                GroupGrouping.Level, _localization.Tr(TranslationKeys.Groups.GroupByLevel)));
            GroupingOptions.Add(new GroupGroupingOption(
                GroupGrouping.Teacher, _localization.Tr(TranslationKeys.Groups.GroupByTeacher)));
            SelectedGrouping = GroupingOptions.First(option => option.Value == previousGrouping);

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

        if (Items.Count > 0)
        {
            RebuildCards();
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
