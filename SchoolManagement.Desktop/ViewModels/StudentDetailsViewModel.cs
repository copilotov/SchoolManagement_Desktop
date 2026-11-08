using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.UseCases;
using SchoolManagement.Desktop.Controls;
using SchoolManagement.Desktop.Imaging;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>
/// Read-only student file opened from the list. Its tabs are filled by a single
/// activity use case so the page never talks to five repositories.
/// </summary>
public sealed partial class StudentDetailsViewModel : PageViewModel, IDisposable
{
    private readonly GetStudentDetailsUseCase _getDetails;
    private readonly GetStudentActivityUseCase _getActivity;
    private readonly RenderStudentQrUseCase _renderQr;
    private readonly ILocalizationService _localization;
    private readonly INavigationService _navigation;

    private bool _disposed;

    public StudentDetailsViewModel(
        GetStudentDetailsUseCase getDetails,
        GetStudentActivityUseCase getActivity,
        RenderStudentQrUseCase renderQr,
        ILocalizationService localization,
        INavigationService navigation)
    {
        _getDetails = getDetails ?? throw new ArgumentNullException(nameof(getDetails));
        _getActivity = getActivity ?? throw new ArgumentNullException(nameof(getActivity));
        _renderQr = renderQr ?? throw new ArgumentNullException(nameof(renderQr));
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

    public string Initials
    {
        get
        {
            var chars = new List<char>(2);
            foreach (var part in new[] { Details?.FirstName, Details?.LastName })
            {
                if (!string.IsNullOrWhiteSpace(part))
                {
                    chars.Add(char.ToUpperInvariant(part.Trim()[0]));
                }
            }

            return new string(chars.ToArray());
        }
    }

    public string StudentCode => Details?.StudentCode ?? string.Empty;

    public StatusTone StatusTone => StatusBadgeMapper.MapTone(Details?.Status ?? string.Empty);

    [ObservableProperty]
    private BitmapSource? _qrImage;

    [ObservableProperty]
    private BitmapSource? _photoImage;

    public bool HasQrImage => QrImage is not null;

    public bool HasPhoto => PhotoImage is not null;

    /// <summary>The group names of the active enrollments, joined for the header.</summary>
    public string PlacementText
    {
        get
        {
            var groups = Enrollments.Select(item => item.GroupName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return groups.Length == 0
                ? _localization.Tr(TranslationKeys.Students.NoGroups)
                : string.Join(" • ", groups);
        }
    }

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

    public string TotalPaidText => Payments
        .Sum(payment => payment.Amount)
        .ToString("0.##", CultureInfo.InvariantCulture);

    public string AttendanceRateText
    {
        get
        {
            if (Attendance.Count == 0)
            {
                return _localization.Tr(TranslationKeys.Common.NoData);
            }

            var present = Attendance.Count(item =>
                string.Equals(item.Status, "present", StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.Status, "late", StringComparison.OrdinalIgnoreCase));
            var rate = present * 100d / Attendance.Count;
            return $"{present} / {Attendance.Count} ({rate:0.#}%)";
        }
    }

    public bool HasEnrollments => Enrollments.Count > 0;

    public bool HasPayments => Payments.Count > 0;

    public bool HasAttendance => Attendance.Count > 0;

    /// <summary>Homework stays a later phase; the tab shows its honest empty state.</summary>
    public bool HasHomework => false;


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
            await LoadImagesAsync(cancellationToken).ConfigureAwait(false);

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

    /// <summary>
    /// Renders the QR of the student code and reads the photo when one exists.
    /// Both are decoration: a failure leaves the header without them instead of
    /// turning the whole file into an error.
    /// </summary>
    private async Task LoadImagesAsync(CancellationToken cancellationToken)
    {
        QrImage = null;

        var qr = await _renderQr.ExecuteAsync(StudentId, cancellationToken).ConfigureAwait(false);
        if (qr.IsSuccess)
        {
            QrImage = BitmapHelper.ToBitmapSource(qr.Value);
        }

        PhotoImage = LoadPhoto(Details?.PhotoPath);
    }

    private static BitmapSource? LoadPhoto(string? path)
    {
        if (path is not { Length: > 0 } || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UriFormatException)
        {
            return null;
        }
    }

    private void RaiseDetailProperties()
    {
        OnPropertyChanged(nameof(FullName));
        OnPropertyChanged(nameof(Initials));
        OnPropertyChanged(nameof(StudentCode));
        OnPropertyChanged(nameof(StatusTone));
        OnPropertyChanged(nameof(DateOfBirthText));
        OnPropertyChanged(nameof(GenderText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(RegistrationFeeText));
        OnPropertyChanged(nameof(HasPhoto));
    }

    private void RaiseTabProperties()
    {
        OnPropertyChanged(nameof(HasEnrollments));
        OnPropertyChanged(nameof(HasPayments));
        OnPropertyChanged(nameof(HasAttendance));
        OnPropertyChanged(nameof(AbsenceText));
        OnPropertyChanged(nameof(TotalPaidText));
        OnPropertyChanged(nameof(AttendanceRateText));
        OnPropertyChanged(nameof(PlacementText));
        OnPropertyChanged(nameof(HasHomework));
    }

    partial void OnQrImageChanged(BitmapSource? value) => OnPropertyChanged(nameof(HasQrImage));

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
