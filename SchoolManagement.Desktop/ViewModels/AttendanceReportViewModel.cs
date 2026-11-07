using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Attendance;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Groups;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Students;
using SchoolManagement.Application.Time;
using SchoolManagement.Application.UseCases;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>
/// Attendance report screen: student/group/period filters, the per-student
/// counters and the PDF/Excel exports. The read use case builds the immutable
/// bundle and the infrastructure port renders it, so the page never references
/// QuestPDF or ClosedXML.
/// </summary>
public sealed partial class AttendanceReportViewModel : PageViewModel, IDisposable
{
    private const string PdfFilter = "PDF (*.pdf)|*.pdf";
    private const string ExcelFilter = "Excel (*.xlsx)|*.xlsx";
    private const string PdfFileName = "attendance-report.pdf";
    private const string ExcelFileName = "attendance-report.xlsx";

    private readonly GetAttendanceReportUseCase _getReport;
    private readonly IAttendanceReportService _reportService;
    private readonly GetGroupsUseCase _getGroups;
    private readonly GetStudentsUseCase _getStudents;
    private readonly IFileDialogService _files;
    private readonly IPdfPrintService _print;
    private readonly ILocalizationService _localization;
    private readonly INavigationService _navigation;
    private readonly ITrustedClock _clock;

    private AttendanceReportData? _data;
    private bool _suppressReload;
    private bool _isLoaded;
    private bool _disposed;

    public AttendanceReportViewModel(
        GetAttendanceReportUseCase getReport,
        IAttendanceReportService reportService,
        GetGroupsUseCase getGroups,
        GetStudentsUseCase getStudents,
        IFileDialogService files,
        IPdfPrintService print,
        ILocalizationService localization,
        INavigationService navigation,
        ITrustedClock clock)
    {
        _getReport = getReport ?? throw new ArgumentNullException(nameof(getReport));
        _reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));
        _getGroups = getGroups ?? throw new ArgumentNullException(nameof(getGroups));
        _getStudents = getStudents ?? throw new ArgumentNullException(nameof(getStudents));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _print = print ?? throw new ArgumentNullException(nameof(print));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

        Rows = new ObservableCollection<AttendanceReportRow>();
        Groups = new ObservableCollection<StudentFilterOption>();
        Students = new ObservableCollection<StudentFilterOption>();

        FromDate = FirstDayOfMonth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        ToDate = Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        _localization.LanguageChanged += OnLanguageChanged;
    }

    public ObservableCollection<AttendanceReportRow> Rows { get; }

    public ObservableCollection<StudentFilterOption> Groups { get; }

    public ObservableCollection<StudentFilterOption> Students { get; }

    public string Title => _localization.Tr(TranslationKeys.Reports.AttendanceReportTitle);

    public DateTime Today => DateTime.SpecifyKind(_clock.LocalNow.Date, DateTimeKind.Utc);

    public DateTime FirstDayOfMonth => new(Today.Year, Today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    [ObservableProperty]
    private StudentFilterOption? _selectedGroup;

    [ObservableProperty]
    private StudentFilterOption? _selectedStudent;

    [ObservableProperty]
    private string _fromDate = string.Empty;

    [ObservableProperty]
    private string _toDate = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isExporting;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    public bool HasRows => Rows.Count > 0;

    public bool IsEmpty => _data is not null && Rows.Count == 0;

    public string PeriodText => _data is null
        ? string.Empty
        : _data.FromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            + " - "
            + _data.ToDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public string GroupText => _data?.GroupName
        ?? _localization.Tr(TranslationKeys.Reports.AllStudents);

    public string StudentText => _data?.StudentName
        ?? _localization.Tr(TranslationKeys.Reports.AllStudents);

    public string TotalStudentsText => CountText(_data?.StudentCount ?? 0);

    public string TotalRecordsText => CountText(_data?.TotalRecords ?? 0);

    public string PresentText => CountText(_data?.TotalPresent ?? 0);

    public string AbsentText => CountText(_data?.TotalAbsent ?? 0);

    public string LateText => CountText(_data?.TotalLate ?? 0);

    public string AttendanceRateText => (_data?.AttendanceRate ?? 0)
        .ToString("0.#", CultureInfo.InvariantCulture) + "%";

    /// <summary>Builds the report query from the filter bar.</summary>
    public AttendanceReportQuery CreateQuery() => new(
        ParseDate(FromDate) ?? Today,
        ParseDate(ToDate) ?? Today,
        SelectedStudent?.Id,
        SelectedGroup?.Id);

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

        await LoadLookupsAsync(cancellationToken).ConfigureAwait(false);
        await LoadAsync(cancellationToken).ConfigureAwait(false);
        _isLoaded = true;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            var result = await _getReport.ExecuteAsync(CreateQuery(), cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                _data = null;
                Rows.Clear();
                RaiseTotals();
                ShowFailure(result.Failure!);
                return;
            }

            _data = result.Value;
            Rows.Clear();
            foreach (var row in result.Value.Rows.OrderBy(row => row.StudentName, StringComparer.Ordinal))
            {
                Rows.Add(row);
            }

            ErrorMessage = string.Empty;
            RaiseTotals();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task SearchAsync() => LoadAsync();

    [RelayCommand]
    private Task ReloadAsync() => LoadAsync();

    [RelayCommand]
    private void Back() => _navigation.NavigateTo<AttendanceViewModel>();

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (_data is null)
        {
            return;
        }

        IsExporting = true;
        try
        {
            var pdf = await _reportService.BuildPdfAsync(_data).ConfigureAwait(false);
            if (pdf.IsFailure)
            {
                ShowFailure(pdf.Failure!);
                return;
            }

            var path = await _files
                .PickSavePathAsync(Title, PdfFilter, PdfFileName)
                .ConfigureAwait(false);

            if (path is null)
            {
                return;
            }

            await _files.WriteAllBytesAsync(path, pdf.Value).ConfigureAwait(false);
            ShowSuccess(TranslationKeys.Reports.ExportSuccess);
        }
        finally
        {
            IsExporting = false;
        }
    }

    [RelayCommand]
    private async Task ExportExcelAsync()
    {
        if (_data is null)
        {
            return;
        }

        IsExporting = true;
        try
        {
            var excel = await _reportService.BuildExcelAsync(_data).ConfigureAwait(false);
            if (excel.IsFailure)
            {
                ShowFailure(excel.Failure!);
                return;
            }

            var path = await _files
                .PickSavePathAsync(Title, ExcelFilter, ExcelFileName)
                .ConfigureAwait(false);

            if (path is null)
            {
                return;
            }

            await _files.WriteAllBytesAsync(path, excel.Value).ConfigureAwait(false);
            ShowSuccess(TranslationKeys.Reports.ExportSuccess);
        }
        finally
        {
            IsExporting = false;
        }
    }

    [RelayCommand]
    private async Task PrintAsync()
    {
        if (_data is null)
        {
            return;
        }

        IsExporting = true;
        try
        {
            var pdf = await _reportService.BuildPdfAsync(_data).ConfigureAwait(false);
            if (pdf.IsFailure)
            {
                ShowFailure(pdf.Failure!);
                return;
            }

            await _print.PrintAsync(pdf.Value, PdfFileName).ConfigureAwait(false);
        }
        finally
        {
            IsExporting = false;
        }
    }

    partial void OnSelectedGroupChanged(StudentFilterOption? value) => _ = RefreshIfLoadedAsync();

    partial void OnSelectedStudentChanged(StudentFilterOption? value) => _ = RefreshIfLoadedAsync();

    partial void OnFromDateChanged(string value) => _ = RefreshIfLoadedAsync();

    partial void OnToDateChanged(string value) => _ = RefreshIfLoadedAsync();

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    private async Task LoadLookupsAsync(CancellationToken cancellationToken)
    {
        var groups = await _getGroups
            .ExecuteAsync(new GroupListQuery(IncludeInactive: true), cancellationToken)
            .ConfigureAwait(false);

        if (groups.IsFailure)
        {
            ShowFailure(groups.Failure!);
            return;
        }

        var students = await _getStudents
            .ExecuteAsync(new StudentListQuery(Take: 1000), cancellationToken)
            .ConfigureAwait(false);

        if (students.IsFailure)
        {
            ShowFailure(students.Failure!);
            return;
        }

        _suppressReload = true;
        try
        {
            var previousGroup = SelectedGroup?.Id;
            var previousStudent = SelectedStudent?.Id;

            Groups.Clear();
            Groups.Add(new StudentFilterOption(null, _localization.Tr(TranslationKeys.Homework.AllGroups)));
            foreach (var group in groups.Value)
            {
                Groups.Add(new StudentFilterOption(group.Id, group.Name));
            }

            Students.Clear();
            Students.Add(new StudentFilterOption(null, _localization.Tr(TranslationKeys.Reports.AllStudents)));
            foreach (var student in students.Value.OrderBy(student => student.LastName, StringComparer.Ordinal))
            {
                Students.Add(new StudentFilterOption(
                    student.Id,
                    student.FirstName + " " + student.LastName));
            }

            SelectedGroup = Groups.FirstOrDefault(option => option.Id == previousGroup) ?? Groups[0];
            SelectedStudent = Students.FirstOrDefault(option => option.Id == previousStudent) ?? Students[0];
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

    private void RaiseTotals()
    {
        OnPropertyChanged(nameof(HasRows));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(PeriodText));
        OnPropertyChanged(nameof(GroupText));
        OnPropertyChanged(nameof(StudentText));
        OnPropertyChanged(nameof(TotalStudentsText));
        OnPropertyChanged(nameof(TotalRecordsText));
        OnPropertyChanged(nameof(PresentText));
        OnPropertyChanged(nameof(AbsentText));
        OnPropertyChanged(nameof(LateText));
        OnPropertyChanged(nameof(AttendanceRateText));
    }

    private string CountText(int count) => count.ToString(CultureInfo.InvariantCulture);

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
    }

    private void ShowSuccess(string messageKey)
    {
        ErrorMessage = string.Empty;
        StatusMessage = _localization.Tr(messageKey);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Title));
        if (Groups.Count > 0)
        {
            Groups[0] = new StudentFilterOption(
                null,
                _localization.Tr(TranslationKeys.Homework.AllGroups));
        }

        if (Students.Count > 0)
        {
            Students[0] = new StudentFilterOption(
                null,
                _localization.Tr(TranslationKeys.Reports.AllStudents));
        }

        RaiseTotals();
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
