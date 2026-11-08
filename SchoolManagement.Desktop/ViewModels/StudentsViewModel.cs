using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Students;
using SchoolManagement.Application.UseCases;
using SchoolManagement.Desktop.Controls;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>One entry of a filter ComboBox. A null id means "all".</summary>
public sealed record StudentFilterOption(int? Id, string Label);

/// <summary>One entry of the sort ComboBox; the label follows the language.</summary>
public sealed record StudentSortOption(StudentSortField Field, string Label);

/// <summary>Gender choice of the shared student form.</summary>
public sealed record StudentGenderOption(string? Value, string Label);

/// <summary>One rejected import row, already localized for the report.</summary>
public sealed record StudentImportErrorItem(string Row, string Reference, string Message);

/// <summary>Table or card presentation of the student list.</summary>
public enum StudentViewMode
{
    Table,
    Cards,
}

/// <summary>
/// One removable filter chip. <see cref="Kind"/> is the filter identifier the
/// <c>ClearFilterCommand</c> switches on; the label is already localized.
/// </summary>
public sealed record StudentFilterChip(string Kind, string Label);

/// <summary>Display-only projection of a student row for the card list.</summary>
public sealed record StudentCardItem(
    StudentListItem Item,
    string FullName,
    string Initials,
    string Placement,
    string GuardianText,
    string FeeText,
    string StatusText,
    StatusTone StatusTone);


/// <summary>
/// Student list screen. It owns the instant filters, the shared add/edit form,
/// the guarded delete and the import/export commands. Everything is routed
/// through use cases; the only UI dependencies are the dialog, file and
/// navigation seams.
/// </summary>
public sealed partial class StudentsViewModel : PageViewModel, IDisposable
{
    private const string CsvFilter = "CSV (*.csv)|*.csv";
    private const string ExcelFilter = "Excel (*.xlsx)|*.xlsx";
    private const string PdfFilter = "PDF (*.pdf)|*.pdf";

    private readonly GetStudentsUseCase _getStudents;
    private readonly GetStudentLookupsUseCase _getLookups;
    private readonly GetStudentDetailsUseCase _getDetails;
    private readonly SaveStudentUseCase _saveStudent;
    private readonly DeleteStudentUseCase _deleteStudent;
    private readonly ImportStudentsUseCase _importStudents;
    private readonly ExportStudentsUseCase _exportStudents;
    private readonly ILocalizationService _localization;
    private readonly IUserDialogService _dialogs;
    private readonly IFileDialogService _files;
    private readonly INavigationService _navigation;
    private readonly ISmsLauncher _sms;
    private readonly LicenseViewModel _license;

    private bool _suppressReload;
    private bool _isLoaded;
    private bool _disposed;
    private IReadOnlyList<StudentListItem> _allItems = Array.Empty<StudentListItem>();

    public StudentsViewModel(
        GetStudentsUseCase getStudents,
        GetStudentLookupsUseCase getLookups,
        GetStudentDetailsUseCase getDetails,
        SaveStudentUseCase saveStudent,
        DeleteStudentUseCase deleteStudent,
        ImportStudentsUseCase importStudents,
        ExportStudentsUseCase exportStudents,
        ILocalizationService localization,
        IUserDialogService dialogs,
        IFileDialogService files,
        INavigationService navigation,
        ISmsLauncher sms,
        LicenseViewModel license)
    {
        _getStudents = getStudents ?? throw new ArgumentNullException(nameof(getStudents));
        _getLookups = getLookups ?? throw new ArgumentNullException(nameof(getLookups));
        _getDetails = getDetails ?? throw new ArgumentNullException(nameof(getDetails));
        _saveStudent = saveStudent ?? throw new ArgumentNullException(nameof(saveStudent));
        _deleteStudent = deleteStudent ?? throw new ArgumentNullException(nameof(deleteStudent));
        _importStudents = importStudents ?? throw new ArgumentNullException(nameof(importStudents));
        _exportStudents = exportStudents ?? throw new ArgumentNullException(nameof(exportStudents));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _sms = sms ?? throw new ArgumentNullException(nameof(sms));
        _license = license ?? throw new ArgumentNullException(nameof(license));

        Items = new ObservableCollection<StudentListItem>();
        Cards = new ObservableCollection<StudentCardItem>();
        ActiveFilters = new ObservableCollection<StudentFilterChip>();
        Levels = new ObservableCollection<StudentFilterOption>();
        Grades = new ObservableCollection<StudentFilterOption>();
        Groups = new ObservableCollection<StudentFilterOption>();
        Statuses = new ObservableCollection<StudentFilterOption>();
        SortOptions = new ObservableCollection<StudentSortOption>();
        Genders = new ObservableCollection<StudentGenderOption>();
        ImportErrors = new ObservableCollection<StudentImportErrorItem>();

        RebuildStaticOptions();
        _localization.LanguageChanged += OnLanguageChanged;
    }

    public ObservableCollection<StudentListItem> Items { get; }

    /// <summary>The same rows, projected for the card list.</summary>
    public ObservableCollection<StudentCardItem> Cards { get; }

    /// <summary>One chip per applied filter; each chip removes only its own filter.</summary>
    public ObservableCollection<StudentFilterChip> ActiveFilters { get; }


    public ObservableCollection<StudentFilterOption> Levels { get; }

    public ObservableCollection<StudentFilterOption> Grades { get; }

    public ObservableCollection<StudentFilterOption> Groups { get; }

    public ObservableCollection<StudentFilterOption> Statuses { get; }

    public ObservableCollection<StudentSortOption> SortOptions { get; }

    public ObservableCollection<StudentGenderOption> Genders { get; }

    public ObservableCollection<StudentImportErrorItem> ImportErrors { get; }

    public LicenseViewModel License => _license;

    [ObservableProperty]
    private UiState<IReadOnlyList<StudentListItem>> _state = new UiState<IReadOnlyList<StudentListItem>>.Initial();

    [ObservableProperty]
    private string? _searchText;

    [ObservableProperty]
    private StudentFilterOption? _selectedLevel;

    [ObservableProperty]
    private StudentFilterOption? _selectedGrade;

    [ObservableProperty]
    private StudentFilterOption? _selectedGroup;

    [ObservableProperty]
    private StudentFilterOption? _selectedStatus;

    [ObservableProperty]
    private StudentSortOption? _selectedSort;

    [ObservableProperty]
    private bool _sortDescending;

    [ObservableProperty]
    private StudentListItem? _selectedItem;

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
    private string _editorFirstName = string.Empty;

    [ObservableProperty]
    private string _editorLastName = string.Empty;

    [ObservableProperty]
    private string _editorCode = string.Empty;

    [ObservableProperty]
    private string _editorDateOfBirth = string.Empty;

    [ObservableProperty]
    private StudentGenderOption? _selectedEditorGender;

    [ObservableProperty]
    private string _editorPhone = string.Empty;

    [ObservableProperty]
    private string _editorAddress = string.Empty;

    [ObservableProperty]
    private StudentFilterOption? _selectedEditorStatus;

    [ObservableProperty]
    private string _editorNotes = string.Empty;

    [ObservableProperty]
    private string _editorRegistrationFee = "0";

    [ObservableProperty]
    private string _editorGuardianName = string.Empty;

    [ObservableProperty]
    private string _editorGuardianPhone = string.Empty;

    [ObservableProperty]
    private string _editorGuardianEmail = string.Empty;

    [ObservableProperty]
    private string _editorGuardianRelationship = string.Empty;

    [ObservableProperty]
    private bool _isImportReportOpen;

    [ObservableProperty]
    private string _importReportTitle = string.Empty;

    [ObservableProperty]
    private string _importReportSummary = string.Empty;

    [ObservableProperty]
    private StudentViewMode _viewMode = StudentViewMode.Table;

    [ObservableProperty]
    private bool _feePendingOnly;

    [ObservableProperty]
    private string _firstNameError = string.Empty;

    [ObservableProperty]
    private string _lastNameError = string.Empty;

    [ObservableProperty]
    private string _guardianError = string.Empty;

    [ObservableProperty]
    private string _feeError = string.Empty;

    /// <summary>
    /// Snapshot of the table selection, pushed by <c>DataTable</c> so the bulk
    /// bar can enable its two actions without the table knowing a student.
    /// </summary>
    [ObservableProperty]
    private IList? _selectedRows;

    public bool HasBulkSelection => SelectedRows is { Count: > 0 };

    public string SelectedCountText => _localization.Tr(
        TranslationKeys.Common.Selected,
        ("count", (SelectedRows?.Count ?? 0).ToString(CultureInfo.InvariantCulture)));

    public bool IsTableView => ViewMode == StudentViewMode.Table;

    public bool IsCardView => ViewMode == StudentViewMode.Cards;

    public bool HasActiveFilters => ActiveFilters.Count > 0;

    public bool HasFirstNameError => !string.IsNullOrEmpty(FirstNameError);

    public bool HasLastNameError => !string.IsNullOrEmpty(LastNameError);

    public bool HasGuardianError => !string.IsNullOrEmpty(GuardianError);

    public bool HasFeeError => !string.IsNullOrEmpty(FeeError);

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    public bool HasImportErrors => ImportErrors.Count > 0;

    public bool IsEmpty => State is UiState<IReadOnlyList<StudentListItem>>.Empty || Items.Count == 0;


    public bool IsLoading => State.IsLoading;

    /// <summary>Builds the filter query sent to the read use case.</summary>
    public StudentListQuery CreateQuery() => new(
        SearchText,
        SelectedStatus?.Id is { } status ? StatusLabelToValue(status) : null,
        SelectedLevel?.Id,
        SelectedGrade?.Id,
        SelectedGroup?.Id,
        SelectedSort?.Field ?? StudentSortField.Name,
        SortDescending);

    /// <summary>Loads the lookup lists and the first page once.</summary>
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
        State = new UiState<IReadOnlyList<StudentListItem>>.Loading();
        var result = await _getStudents.ExecuteAsync(CreateQuery(), cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            _allItems = Array.Empty<StudentListItem>();
            Items.Clear();
            Cards.Clear();
            State = new UiState<IReadOnlyList<StudentListItem>>.Error(result.Failure!);
            ShowFailure(result.Failure!);
            return;
        }

        _allItems = result.Value;
        var visible = FeePendingOnly
            ? result.Value.Where(item => item.RegistrationFee > 0).ToList()
            : result.Value.ToList();

        Items.Clear();
        foreach (var item in visible)
        {
            Items.Add(item);
        }

        RebuildCards();
        RebuildActiveFilters();

        State = result.Value.Count == 0
            ? new UiState<IReadOnlyList<StudentListItem>>.Empty()
            : new UiState<IReadOnlyList<StudentListItem>>.Success(result.Value);

        // A reload deliberately keeps the last status banner: the Phase 4 lists
        // behave the same way, and a save/import message must survive the refresh
        // that follows it. Explicit actions (Add, Edit, failures) clear it instead.
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasActiveFilters));
    }

    /// <summary>Maps a <see cref="StudentDetails"/> row onto the shared form.</summary>
    public void ApplyDetails(StudentDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        EditingId = details.Id;
        EditorFirstName = details.FirstName;
        EditorLastName = details.LastName;
        EditorCode = details.StudentCode;
        EditorDateOfBirth = details.DateOfBirth is { } date
            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : string.Empty;
        SelectedEditorGender = Genders.FirstOrDefault(option =>
            string.Equals(option.Value, details.Gender, StringComparison.OrdinalIgnoreCase));
        EditorPhone = details.Phone ?? string.Empty;
        EditorAddress = details.Address ?? string.Empty;
        SelectedEditorStatus = Statuses.FirstOrDefault(option => option.Id == StatusValueToId(details.Status));
        EditorNotes = details.Notes ?? string.Empty;
        EditorRegistrationFee = details.RegistrationFee.ToString(CultureInfo.InvariantCulture);
        EditorGuardianName = details.GuardianName ?? string.Empty;
        EditorGuardianPhone = details.GuardianPhone ?? string.Empty;
        EditorGuardianEmail = details.GuardianEmail ?? string.Empty;
        EditorGuardianRelationship = details.GuardianRelationship ?? string.Empty;
    }

    /// <summary>Collects the form into the request consumed by the save use case.</summary>
    public StudentSaveRequest CreateSaveRequest()
    {
        DateTime? dateOfBirth = null;
        if (!string.IsNullOrWhiteSpace(EditorDateOfBirth))
        {
            dateOfBirth = DateTime.TryParseExact(
                EditorDateOfBirth.Trim(),
                new[] { "yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy" },
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed)
                ? DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc)
                : null;
        }

        var fee = double.TryParse(
            EditorRegistrationFee,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsedFee)
            ? parsedFee
            : 0;

        return new StudentSaveRequest(
            EditorFirstName,
            EditorLastName,
            string.IsNullOrWhiteSpace(EditorCode) ? null : EditorCode,
            dateOfBirth,
            SelectedEditorGender?.Value,
            EditorPhone,
            EditorAddress,
            SelectedEditorStatus?.Id is { } status ? StatusLabelToValue(status) : StudentStatuses.Active,
            EditorNotes,
            fee,
            EditorGuardianName,
            EditorGuardianPhone,
            EditorGuardianEmail,
            EditorGuardianRelationship,
            StudentId: EditingId);
    }

    /// <summary>Opens the shared form for the given row, used by the details page.</summary>
    public Task BeginEditAsync(int studentId) => OpenEditorAsync(studentId);

    [RelayCommand]
    private Task SearchAsync() => RefreshAsync();

    [RelayCommand]
    private Task ReloadAsync() => RefreshAsync();

    [RelayCommand]
    private async Task AddAsync()
    {
        ClearMessages();
        ResetEditor();
        EditorTitle = _localization.Tr(TranslationKeys.Students.Add);
        SelectedEditorStatus = Statuses.FirstOrDefault(option => option.Id == 1);
        IsEditorOpen = true;
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task EditAsync(StudentListItem? item)
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
    private async Task DeleteAsync(StudentListItem? item)
    {
        var target = item ?? SelectedItem;
        if (target is null)
        {
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            _localization.Tr(TranslationKeys.Students.Title),
            _localization.Tr(
                TranslationKeys.Students.DeleteMessage,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["name"] = $"{target.FirstName} {target.LastName}".Trim(),
                }),
            _localization.Tr(TranslationKeys.Common.Delete),
            _localization.Tr(TranslationKeys.Common.Cancel)).ConfigureAwait(false);

        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _deleteStudent.ExecuteAsync(target.Id).ConfigureAwait(false);
            if (result.IsFailure)
            {
                ShowFailure(result.Failure!);
                return;
            }

            ShowSuccess(TranslationKeys.Messages.DeleteSuccess);
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
    private Task DetailsAsync(StudentListItem? item)
        => OpenDetailsAsync(item ?? SelectedItem);

    [RelayCommand]
    private Task PrintCardAsync(StudentListItem? item)
        => OpenCardAsync(item ?? SelectedItem);

    [RelayCommand]
    private async Task OpenQrPrintAsync()
    {
        var page = _navigation.NavigateTo<StudentsQrPrintViewModel>();
        await page.InitializeAsync(CreateQuery()).ConfigureAwait(false);
    }

    [RelayCommand]
    private void ShowTable() => ViewMode = StudentViewMode.Table;

    [RelayCommand]
    private void ShowCards() => ViewMode = StudentViewMode.Cards;

    /// <summary>Removes a single chip; every other filter stays untouched.</summary>
    [RelayCommand]
    private void ClearFilter(string? kind)
    {
        switch (kind)
        {
            case "search":
                SearchText = string.Empty;
                break;
            case "level":
                SelectedLevel = Levels.FirstOrDefault();
                break;
            case "grade":
                SelectedGrade = Grades.FirstOrDefault();
                break;
            case "group":
                SelectedGroup = Groups.FirstOrDefault();
                break;
            case "status":
                SelectedStatus = Statuses.FirstOrDefault();
                break;
            case "fee":
                FeePendingOnly = false;
                break;
            default:
                break;
        }
    }

    [RelayCommand]
    private async Task ClearFiltersAsync()
    {
        _suppressReload = true;
        try
        {
            SearchText = null;
            SelectedLevel = Levels.FirstOrDefault();
            SelectedGrade = Grades.FirstOrDefault();
            SelectedGroup = Groups.FirstOrDefault();
            SelectedStatus = Statuses.FirstOrDefault();
            FeePendingOnly = false;
        }
        finally
        {
            _suppressReload = false;
        }

        await RefreshIfLoadedAsync().ConfigureAwait(false);
    }

    /// <summary>Prints the QR sheet of the rows ticked in the table.</summary>
    [RelayCommand]
    private async Task PrintSelectedQrAsync()
    {
        var ids = SelectedStudentIds();
        if (ids.Count == 0)
        {
            return;
        }

        var page = _navigation.NavigateTo<StudentsQrPrintViewModel>();
        await page.InitializeAsync(ids).ConfigureAwait(false);
    }

    /// <summary>
    /// Opens the messaging app for the guardians of the ticked rows. Nothing
    /// leaves the machine: the body is localized locally and the numbers are
    /// passed to the <c>sms:</c> launcher.
    /// </summary>
    [RelayCommand]
    private async Task MessageGuardiansAsync()
    {
        var rows = SelectedStudents();
        if (rows.Count == 0)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var phones = new List<string>();
            var names = new List<string>();

            foreach (var row in rows)
            {
                var details = await _getDetails.ExecuteAsync(row.Id).ConfigureAwait(false);
                if (details.IsFailure)
                {
                    ShowFailure(details.Failure!);
                    return;
                }

                var phone = details.Value?.GuardianPhone;
                if (string.IsNullOrWhiteSpace(phone))
                {
                    phone = row.Phone;
                }

                if (!string.IsNullOrWhiteSpace(phone))
                {
                    phones.Add(phone);
                    names.Add($"{row.FirstName} {row.LastName}".Trim());
                }
            }

            if (phones.Count == 0)
            {
                ErrorMessage = _localization.Tr(TranslationKeys.Students.MessageNoPhone);
                StatusMessage = string.Empty;
                IsRenewVisible = false;
                return;
            }

            var body = _localization.Tr(
                TranslationKeys.Students.MessageBody,
                ("name", names.Count == 1 ? names[0] : string.Join(", ", names)));

            var opened = await _sms.OpenAsync(phones, body).ConfigureAwait(false);
            if (opened)
            {
                ShowSuccess(TranslationKeys.Students.MessageOpened, ("count", phones.Count));
            }
            else
            {
                ErrorMessage = _localization.Tr(TranslationKeys.Students.MessageFailed);
                StatusMessage = string.Empty;
                IsRenewVisible = false;
            }
        }
        finally
        {
            IsBusy = false;
        }
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
            var result = await _saveStudent.ExecuteAsync(CreateSaveRequest()).ConfigureAwait(false);
            if (result.IsFailure)
            {
                ShowFailure(result.Failure!);
                return;
            }

            IsEditorOpen = false;
            ShowSuccess(TranslationKeys.Messages.SavedSuccessfully);
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
    private async Task ImportCsvAsync() => await ImportAsync(StudentFileFormat.Csv).ConfigureAwait(false);

    [RelayCommand]
    private async Task ImportExcelAsync() => await ImportAsync(StudentFileFormat.Excel).ConfigureAwait(false);

    [RelayCommand]
    private async Task ExportCsvAsync() => await ExportAsync(StudentFileFormat.Csv).ConfigureAwait(false);

    [RelayCommand]
    private async Task ExportExcelAsync() => await ExportAsync(StudentFileFormat.Excel).ConfigureAwait(false);

    [RelayCommand]
    private void CloseImportReport()
    {
        IsImportReportOpen = false;
        ImportErrors.Clear();
        OnPropertyChanged(nameof(HasImportErrors));
    }

    [RelayCommand]
    private async Task RenewLicenseAsync()
    {
        await _license.RenewInteractiveAsync().ConfigureAwait(false);
        await RefreshAsync().ConfigureAwait(false);
    }

    partial void OnSearchTextChanged(string? value) => _ = RefreshIfLoadedAsync();

    partial void OnSelectedLevelChanged(StudentFilterOption? value) => _ = RefreshIfLoadedAsync();

    partial void OnSelectedGradeChanged(StudentFilterOption? value) => _ = RefreshIfLoadedAsync();

    partial void OnSelectedGroupChanged(StudentFilterOption? value) => _ = RefreshIfLoadedAsync();

    partial void OnSelectedStatusChanged(StudentFilterOption? value) => _ = RefreshIfLoadedAsync();

    partial void OnSelectedSortChanged(StudentSortOption? value) => _ = RefreshIfLoadedAsync();

    partial void OnSortDescendingChanged(bool value) => _ = RefreshIfLoadedAsync();

    partial void OnFeePendingOnlyChanged(bool value) => _ = RefreshIfLoadedAsync();

    partial void OnViewModeChanged(StudentViewMode value)
    {
        OnPropertyChanged(nameof(IsTableView));
        OnPropertyChanged(nameof(IsCardView));
    }

    partial void OnSelectedRowsChanged(IList? value)
    {
        OnPropertyChanged(nameof(HasBulkSelection));
        OnPropertyChanged(nameof(SelectedCountText));
    }

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    partial void OnFirstNameErrorChanged(string value) => OnPropertyChanged(nameof(HasFirstNameError));

    partial void OnLastNameErrorChanged(string value) => OnPropertyChanged(nameof(HasLastNameError));

    partial void OnGuardianErrorChanged(string value) => OnPropertyChanged(nameof(HasGuardianError));

    partial void OnFeeErrorChanged(string value) => OnPropertyChanged(nameof(HasFeeError));

    partial void OnEditorFirstNameChanged(string value) => FirstNameError = string.Empty;

    partial void OnEditorLastNameChanged(string value) => LastNameError = string.Empty;

    partial void OnEditorGuardianNameChanged(string value) => GuardianError = string.Empty;

    partial void OnEditorRegistrationFeeChanged(string value) => FeeError = string.Empty;

    /// <summary>
    /// Validates the form locally so an obvious mistake never reaches the use
    /// case. The use case keeps its own rules; this only short-circuits the
    /// round-trip and puts the message under the right field.
    /// </summary>
    public bool ValidateEditor()
    {
        FirstNameError = string.IsNullOrWhiteSpace(EditorFirstName)
            ? _localization.Tr(TranslationKeys.Students.ValidationFirstName)
            : string.Empty;

        LastNameError = string.IsNullOrWhiteSpace(EditorLastName)
            ? _localization.Tr(TranslationKeys.Students.ValidationLastName)
            : string.Empty;

        var hasGuardianContact = !string.IsNullOrWhiteSpace(EditorGuardianPhone)
            || !string.IsNullOrWhiteSpace(EditorGuardianEmail);
        GuardianError = hasGuardianContact && string.IsNullOrWhiteSpace(EditorGuardianName)
            ? _localization.Tr(TranslationKeys.Students.ValidationGuardian)
            : string.Empty;

        var type = NumberStyles.Float;
        var culture = CultureInfo.InvariantCulture;
        FeeError = !string.IsNullOrWhiteSpace(EditorRegistrationFee)
            && (!double.TryParse(EditorRegistrationFee, type, culture, out var fee) || fee < 0)
                ? _localization.Tr(TranslationKeys.Students.ValidationFee)
                : string.Empty;

        return !HasFirstNameError && !HasLastNameError && !HasGuardianError && !HasFeeError;
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
        try
        {
            var previousLevel = SelectedLevel?.Id;
            var previousGrade = SelectedGrade?.Id;
            var previousGroup = SelectedGroup?.Id;

            FillFilter(Levels, result.Value.Levels, _localization.Tr(TranslationKeys.Students.AllLevels));
            FillFilter(Grades, result.Value.Grades, _localization.Tr(TranslationKeys.Students.AllGrades));
            FillFilter(Groups, result.Value.Groups, _localization.Tr(TranslationKeys.Students.AllGroups));

            SelectedLevel = Levels.FirstOrDefault(option => option.Id == previousLevel) ?? Levels.FirstOrDefault();
            SelectedGrade = Grades.FirstOrDefault(option => option.Id == previousGrade) ?? Grades.FirstOrDefault();
            SelectedGroup = Groups.FirstOrDefault(option => option.Id == previousGroup) ?? Groups.FirstOrDefault();
        }
        finally
        {
            _suppressReload = false;
        }
    }

    private async Task OpenEditorAsync(int studentId)
    {
        IsBusy = true;
        try
        {
            var result = await _getDetails.ExecuteAsync(studentId).ConfigureAwait(false);
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

            ClearMessages();
            ApplyDetails(result.Value);
            EditorTitle = _localization.Tr(TranslationKeys.Students.Edit);
            IsEditorOpen = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task OpenDetailsAsync(StudentListItem? item)
    {
        if (item is null)
        {
            return;
        }

        var page = _navigation.NavigateTo<StudentDetailsViewModel>();
        await page.InitializeAsync(item.Id).ConfigureAwait(false);
    }

    private async Task OpenCardAsync(StudentListItem? item)
    {
        if (item is null)
        {
            return;
        }

        var page = _navigation.NavigateTo<StudentCardViewModel>();
        await page.InitializeAsync(item.Id).ConfigureAwait(false);
    }

    private async Task ImportAsync(StudentFileFormat format)
    {
        var picked = await _files.OpenAsync(
            _localization.Tr(TranslationKeys.Students.Import),
            format == StudentFileFormat.Csv ? CsvFilter : ExcelFilter).ConfigureAwait(false);

        if (picked is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _importStudents
                .ExecuteAsync(new StudentImportRequest(format, picked.Content))
                .ConfigureAwait(false);

            if (result.IsFailure)
            {
                ShowFailure(result.Failure!);
                return;
            }

            ShowImportReport(result.Value);
            await RefreshAsync().ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExportAsync(StudentFileFormat format)
    {
        IsBusy = true;
        try
        {
            var result = await _exportStudents.ExecuteAsync(format).ConfigureAwait(false);
            if (result.IsFailure)
            {
                ShowFailure(result.Failure!);
                return;
            }

            var path = await _files.PickSavePathAsync(
                _localization.Tr(TranslationKeys.Students.Export),
                format == StudentFileFormat.Csv ? CsvFilter : ExcelFilter,
                result.Value.FileName).ConfigureAwait(false);

            if (path is null)
            {
                return;
            }

            await _files.WriteAllBytesAsync(path, result.Value.Content).ConfigureAwait(false);
            ShowSuccess(TranslationKeys.Students.ExportSuccess, ("count", result.Value.Count));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RebuildCards()
    {
        Cards.Clear();
        foreach (var item in Items)
        {
            Cards.Add(new StudentCardItem(
                item,
                string.Join(" ", new[] { item.FirstName, item.LastName }.Where(part => !string.IsNullOrWhiteSpace(part))),
                Initials(item.FirstName, item.LastName),
                PlacementText(item),
                item.GuardianName ?? _localization.Tr(TranslationKeys.Common.NotSpecified),
                item.RegistrationFee.ToString("0.##", CultureInfo.InvariantCulture),
                StatusLabel(item.Status),
                StatusToneFor(item.Status)));
        }
    }

    private string PlacementText(StudentListItem item)
    {
        var parts = new[] { item.LevelName, item.GradeName, item.GroupNames }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        return parts.Length == 0
            ? _localization.Tr(TranslationKeys.Common.NotSpecified)
            : string.Join(" • ", parts);
    }

    private static string Initials(string first, string last)
    {
        var chars = new List<char>(2);
        foreach (var part in new[] { first, last })
        {
            if (!string.IsNullOrWhiteSpace(part))
            {
                chars.Add(char.ToUpperInvariant(part.Trim()[0]));
            }
        }

        return new string(chars.ToArray());
    }

    private string StatusLabel(string status) => status switch
    {
        StudentStatuses.Active => _localization.Tr(TranslationKeys.Students.Active),
        StudentStatuses.Inactive => _localization.Tr(TranslationKeys.Students.Inactive),
        StudentStatuses.Withdrawn => _localization.Tr(TranslationKeys.Students.Withdrawn),
        _ => status,
    };

    internal static StatusTone StatusToneFor(string status) => StatusBadgeMapper.MapTone(status);

    /// <summary>
    /// Rebuilds the chip strip from the live filter state. Called after every
    /// load and after a language switch, so a chip never shows a stale label.
    /// </summary>
    private void RebuildActiveFilters()
    {
        ActiveFilters.Clear();

        void Add(string kind, string? label)
        {
            if (!string.IsNullOrWhiteSpace(label))
            {
                ActiveFilters.Add(new StudentFilterChip(kind, label));
            }
        }

        Add("search", SearchText);
        Add("level", SelectedLevel?.Id is null ? null : SelectedLevel.Label);
        Add("grade", SelectedGrade?.Id is null ? null : SelectedGrade.Label);
        Add("group", SelectedGroup?.Id is null ? null : SelectedGroup.Label);
        Add("status", SelectedStatus?.Id is null ? null : SelectedStatus.Label);
        Add("fee", FeePendingOnly ? _localization.Tr(TranslationKeys.Students.FeePendingFilter) : null);

        OnPropertyChanged(nameof(HasActiveFilters));
    }

    private List<StudentListItem> SelectedStudents()
        => SelectedRows?.OfType<StudentListItem>().ToList() ?? new List<StudentListItem>();

    private List<int> SelectedStudentIds()
        => SelectedStudents().Select(item => item.Id).ToList();

    private void ShowImportReport(StudentImportResult result)    {
        ImportErrors.Clear();
        foreach (var error in result.Errors)
        {
            ImportErrors.Add(new StudentImportErrorItem(
                _localization.Tr(
                    TranslationKeys.Students.ImportRow,
                    ("row", error.RowNumber.ToString(CultureInfo.InvariantCulture))),
                error.Reference,
                _localization.Tr(error.MessageKey, error.Parameters)));
        }

        ImportReportTitle = _localization.Tr(TranslationKeys.Students.ImportReport);
        ImportReportSummary = _localization.Tr(
            TranslationKeys.Students.ImportSuccess,
            ("count", result.Created.ToString(CultureInfo.InvariantCulture)));
        IsImportReportOpen = ImportErrors.Count > 0;
        OnPropertyChanged(nameof(HasImportErrors));

        if (result.Created > 0)
        {
            StatusMessage = ImportReportSummary;
            ErrorMessage = string.Empty;
        }
    }

    private void ResetEditor()
    {
        EditingId = null;
        EditorFirstName = string.Empty;
        EditorLastName = string.Empty;
        EditorCode = string.Empty;
        EditorDateOfBirth = string.Empty;
        SelectedEditorGender = null;
        EditorPhone = string.Empty;
        EditorAddress = string.Empty;
        SelectedEditorStatus = null;
        EditorNotes = string.Empty;
        EditorRegistrationFee = "0";
        EditorGuardianName = string.Empty;
        EditorGuardianPhone = string.Empty;
        EditorGuardianEmail = string.Empty;
        EditorGuardianRelationship = string.Empty;
        FirstNameError = string.Empty;
        LastNameError = string.Empty;
        GuardianError = string.Empty;
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

    private static void FillFilter(
        ObservableCollection<StudentFilterOption> target,
        IReadOnlyList<LookupItem> items,
        string allLabel)
    {
        target.Clear();
        target.Add(new StudentFilterOption(null, allLabel));
        foreach (var item in items)
        {
            target.Add(new StudentFilterOption(item.Id, item.Name));
        }
    }

    private static string StatusLabelToValue(int? id) => id switch
    {
        1 => StudentStatuses.Active,
        2 => StudentStatuses.Inactive,
        3 => StudentStatuses.Withdrawn,
        _ => StudentStatuses.Active,
    };

    private static int? StatusValueToId(string? status) => status switch
    {
        StudentStatuses.Active => 1,
        StudentStatuses.Inactive => 2,
        StudentStatuses.Withdrawn => 3,
        _ => null,
    };

    private void RebuildStaticOptions()
    {
        _suppressReload = true;
        try
        {
            Statuses.Clear();
            Statuses.Add(new StudentFilterOption(null, _localization.Tr(TranslationKeys.Students.AllStatuses)));
            Statuses.Add(new StudentFilterOption(1, _localization.Tr(TranslationKeys.Students.Active)));
            Statuses.Add(new StudentFilterOption(2, _localization.Tr(TranslationKeys.Students.Inactive)));
            Statuses.Add(new StudentFilterOption(3, _localization.Tr(TranslationKeys.Students.Withdrawn)));

            Genders.Clear();
            Genders.Add(new StudentGenderOption(null, _localization.Tr(TranslationKeys.Common.NotSpecified)));
            Genders.Add(new StudentGenderOption("male", _localization.Tr(TranslationKeys.Students.Male)));
            Genders.Add(new StudentGenderOption("female", _localization.Tr(TranslationKeys.Students.Female)));

            var previousSort = SelectedSort?.Field ?? StudentSortField.Name;
            SortOptions.Clear();
            SortOptions.Add(new StudentSortOption(StudentSortField.Name, _localization.Tr(TranslationKeys.Students.SortName)));
            SortOptions.Add(new StudentSortOption(StudentSortField.Code, _localization.Tr(TranslationKeys.Students.SortCode)));
            SortOptions.Add(new StudentSortOption(StudentSortField.Level, _localization.Tr(TranslationKeys.Students.SortLevel)));
            SortOptions.Add(new StudentSortOption(StudentSortField.Group, _localization.Tr(TranslationKeys.Students.SortGroup)));
            SortOptions.Add(new StudentSortOption(StudentSortField.Status, _localization.Tr(TranslationKeys.Students.SortStatus)));
            SelectedSort = SortOptions.First(option => option.Field == previousSort);

            var previousStatus = SelectedStatus?.Id;
            SelectedStatus = Statuses.FirstOrDefault(option => option.Id == previousStatus) ?? Statuses[0];
        }
        finally
        {
            _suppressReload = false;
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RebuildStaticOptions();
        var allLevel = _localization.Tr(TranslationKeys.Students.AllLevels);
        var allGrade = _localization.Tr(TranslationKeys.Students.AllGrades);
        var allGroup = _localization.Tr(TranslationKeys.Students.AllGroups);
        if (Levels.Count > 0)
        {
            Levels[0] = new StudentFilterOption(null, allLevel);
        }

        if (Grades.Count > 0)
        {
            Grades[0] = new StudentFilterOption(null, allGrade);
        }

        if (Groups.Count > 0)
        {
            Groups[0] = new StudentFilterOption(null, allGroup);
        }

        if (IsEditorOpen)
        {
            EditorTitle = _localization.Tr(
                EditingId is null ? TranslationKeys.Students.Add : TranslationKeys.Students.Edit);
        }

        if (_allItems.Count > 0)
        {
            RebuildCards();
        }

        RebuildActiveFilters();
        OnPropertyChanged(nameof(SelectedCountText));
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
