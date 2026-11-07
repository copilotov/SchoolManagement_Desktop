using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.UseCases;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>
/// Read-only student file opened from the list. Its three tabs are filled by a
/// single activity use case so the page never talks to five repositories.
/// </summary>
public sealed partial class StudentDetailsViewModel : PageViewModel, IDisposable
{
    private readonly GetStudentDetailsUseCase _getDetails;
    private readonly GetStudentActivityUseCase _getActivity;
    private readonly ILocalizationService _localization;
    private readonly INavigationService _navigation;

    private bool _disposed;

    public StudentDetailsViewModel(
        GetStudentDetailsUseCase getDetails,
        GetStudentActivityUseCase getActivity,
        ILocalizationService localization,
        INavigationService navigation)
    {
        _getDetails = getDetails ?? throw new ArgumentNullException(nameof(getDetails));
        _getActivity = getActivity ?? throw new ArgumentNullException(nameof(getActivity));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));

        Enrollments = new ObservableCollection<EnrollmentListItem>();
        Payments = new ObservableCollection<PaymentListItem>();
        Attendance = new ObservableCollection<AttendanceListItem>();

        _localization.LanguageChanged += OnLanguageChanged;
    }

    public ObservableCollection<EnrollmentListItem> Enrollments { get; }

    public ObservableCollection<PaymentListItem> Payments { get; }

    public ObservableCollection<AttendanceListItem> Attendance { get; }

    [ObservableProperty]
    private StudentDetails? _details;

    [ObservableProperty]
    private StudentAbsenceSummary? _absenceSummary;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private int _studentId;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public string FullName => Details is null ? string.Empty : $"{Details.FirstName} {Details.LastName}".Trim();

    public string DateOfBirthText => Details?.DateOfBirth is { } date
        ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        : _localization.Tr(TranslationKeys.Common.NotSpecified);

    public string GenderText => Details?.Gender switch
    {
        "male" => _localization.Tr(TranslationKeys.Students.Male),
        "female" => _localization.Tr(TranslationKeys.Students.Female),
        _ => _localization.Tr(TranslationKeys.Common.NotSpecified),
    };

    public string StatusText => Details?.Status switch
    {
        "active" => _localization.Tr(TranslationKeys.Students.Active),
        "inactive" => _localization.Tr(TranslationKeys.Students.Inactive),
        "withdrawn" => _localization.Tr(TranslationKeys.Students.Withdrawn),
        _ => Details?.Status ?? string.Empty,
    };

    public string RegistrationFeeText => Details is null
        ? string.Empty
        : Details.RegistrationFee.ToString("0.##", CultureInfo.InvariantCulture);

    public string AbsenceText => AbsenceSummary is null
        ? string.Empty
        : $"{AbsenceSummary.AbsenceCount} / {AbsenceSummary.Limit}";

    public bool HasEnrollments => Enrollments.Count > 0;

    public bool HasPayments => Payments.Count > 0;

    public bool HasAttendance => Attendance.Count > 0;

    public async Task InitializeAsync(int studentId, CancellationToken cancellationToken = default)
    {
        StudentId = studentId;
        await LoadAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            var details = await _getDetails.ExecuteAsync(StudentId, cancellationToken).ConfigureAwait(false);
            if (details.IsFailure)
            {
                ShowFailure(details.Failure!);
                return;
            }

            if (details.Value is null)
            {
                ShowFailure(Failure.NotFound(TranslationKeys.Errors.NotFound));
                return;
            }

            Details = details.Value;
            RaiseDetailProperties();

            var activity = await _getActivity.ExecuteAsync(StudentId, cancellationToken).ConfigureAwait(false);
            if (activity.IsFailure)
            {
                ShowFailure(activity.Failure!);
                return;
            }

            Enrollments.Clear();
            foreach (var enrollment in activity.Value.Enrollments)
            {
                Enrollments.Add(enrollment);
            }

            Payments.Clear();
            foreach (var payment in activity.Value.Payments)
            {
                Payments.Add(payment);
            }

            Attendance.Clear();
            foreach (var record in activity.Value.Attendance)
            {
                Attendance.Add(record);
            }

            AbsenceSummary = activity.Value.AbsenceSummary;
            ErrorMessage = string.Empty;
            RaiseTabProperties();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Back() => _navigation.NavigateToTab(ShellTab.Students);

    [RelayCommand]
    private async Task EditAsync()
    {
        var students = _navigation.NavigateTo<StudentsViewModel>();
        await students.BeginEditAsync(StudentId).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task PrintCardAsync()
    {
        var page = _navigation.NavigateTo<StudentCardViewModel>();
        await page.InitializeAsync(StudentId).ConfigureAwait(false);
    }

    [RelayCommand]
    private void OpenGroups() => _navigation.NavigateToTab(ShellTab.Groups);

    [RelayCommand]
    private void OpenFinance() => _navigation.NavigateToTab(ShellTab.Finance);

    private void ShowFailure(Failure failure)
    {
        ErrorMessage = failure.ToDisplayMessage(_localization);
    }

    private void RaiseDetailProperties()
    {
        OnPropertyChanged(nameof(FullName));
        OnPropertyChanged(nameof(DateOfBirthText));
        OnPropertyChanged(nameof(GenderText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(RegistrationFeeText));
    }

    private void RaiseTabProperties()
    {
        OnPropertyChanged(nameof(HasEnrollments));
        OnPropertyChanged(nameof(HasPayments));
        OnPropertyChanged(nameof(HasAttendance));
        OnPropertyChanged(nameof(AbsenceText));
    }

    partial void OnDetailsChanged(StudentDetails? value) => RaiseDetailProperties();

    partial void OnAbsenceSummaryChanged(StudentAbsenceSummary? value) => OnPropertyChanged(nameof(AbsenceText));

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RaiseDetailProperties();
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
