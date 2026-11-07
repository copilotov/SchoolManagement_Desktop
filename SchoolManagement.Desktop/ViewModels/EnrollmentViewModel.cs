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

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>
/// Enrollment board of one group: the active roster on the right, the active
/// students that can still join on the left. A full group only asks for a
/// confirmation; the capacity policy never blocks an enrollment.
/// </summary>
public sealed partial class EnrollmentViewModel : PageViewModel, IDisposable
{
    private readonly GetEnrollmentBoardUseCase _getBoard;
    private readonly EnrollStudentInGroupUseCase _enroll;
    private readonly WithdrawEnrollmentUseCase _withdraw;
    private readonly ILocalizationService _localization;
    private readonly IUserDialogService _dialogs;
    private readonly INavigationService _navigation;
    private readonly LicenseViewModel _license;

    private bool _isLoaded;
    private bool _disposed;

    public EnrollmentViewModel(
        GetEnrollmentBoardUseCase getBoard,
        EnrollStudentInGroupUseCase enroll,
        WithdrawEnrollmentUseCase withdraw,
        ILocalizationService localization,
        IUserDialogService dialogs,
        INavigationService navigation,
        LicenseViewModel license)
    {
        _getBoard = getBoard ?? throw new ArgumentNullException(nameof(getBoard));
        _enroll = enroll ?? throw new ArgumentNullException(nameof(enroll));
        _withdraw = withdraw ?? throw new ArgumentNullException(nameof(withdraw));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _license = license ?? throw new ArgumentNullException(nameof(license));

        Enrolled = new ObservableCollection<EnrollmentListItem>();
        Available = new ObservableCollection<StudentListItem>();

        _localization.LanguageChanged += OnLanguageChanged;
    }

    public ObservableCollection<EnrollmentListItem> Enrolled { get; }

    public ObservableCollection<StudentListItem> Available { get; }

    public string Title => _localization.Tr(TranslationKeys.Enrollment.Title);

    [ObservableProperty]
    private int _groupId;

    [ObservableProperty]
    private GroupSummary? _group;

    [ObservableProperty]
    private string? _searchText;

    [ObservableProperty]
    private EnrollmentListItem? _selectedEnrolled;

    [ObservableProperty]
    private StudentListItem? _selectedAvailable;

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

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    public bool HasAvailable => Available.Count > 0;

    public bool HasEnrolled => Enrolled.Count > 0;

    public string GroupName => Group?.Name ?? string.Empty;

    public string SubjectText => Group?.SubjectName ?? _localization.Tr(TranslationKeys.Common.NotSpecified);

    public string TeacherText => Group?.TeacherName ?? _localization.Tr(TranslationKeys.Groups.NoTeacher);

    public bool IsFull => Group?.IsFull ?? false;

    public string CapacityText => Group is null
        ? string.Empty
        : _localization.Tr(
            TranslationKeys.Enrollment.CapacityLabel,
            ("enrolled", Group.ActiveEnrollments.ToString(CultureInfo.InvariantCulture)),
            ("max", Group.MaxStudents.ToString(CultureInfo.InvariantCulture)));

    public int CapacityPercent => Group is null || Group.MaxStudents <= 0
        ? 0
        : Math.Clamp((int)Math.Round(Group.ActiveEnrollments * 100d / Group.MaxStudents), 0, 100);

    public async Task InitializeAsync(int groupId, CancellationToken cancellationToken = default)
    {
        GroupId = groupId;
        await LoadAsync(cancellationToken).ConfigureAwait(false);
        _isLoaded = true;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            var result = await _getBoard
                .ExecuteAsync(GroupId, SearchText, cancellationToken)
                .ConfigureAwait(false);

            if (result.IsFailure)
            {
                ShowFailure(result.Failure!);
                return;
            }

            Enrolled.Clear();
            foreach (var enrollment in result.Value.Enrolled)
            {
                Enrolled.Add(enrollment);
            }

            Available.Clear();
            foreach (var student in result.Value.Available)
            {
                Available.Add(student);
            }

            Group = result.Value.Group;
            ErrorMessage = string.Empty;
            RaiseBoardProperties();
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
    private async Task EnrollAsync(StudentListItem? item)
    {
        var student = item ?? SelectedAvailable;
        if (student is null || Group is null)
        {
            return;
        }

        ClearMessages();

        if (Group.IsFull)
        {
            var confirmed = await _dialogs.ConfirmAsync(
                _localization.Tr(TranslationKeys.Enrollment.CapacityExceededTitle),
                _localization.Tr(
                    TranslationKeys.Enrollment.CapacityExceededMessage,
                    ("max", Group.MaxStudents.ToString(CultureInfo.InvariantCulture)),
                    ("enrolled", Group.ActiveEnrollments.ToString(CultureInfo.InvariantCulture))),
                _localization.Tr(TranslationKeys.Common.Confirm),
                _localization.Tr(TranslationKeys.Common.Cancel)).ConfigureAwait(false);

            if (!confirmed)
            {
                return;
            }
        }

        IsBusy = true;
        try
        {
            var result = await _enroll
                .ExecuteAsync(new EnrollmentSaveRequest(GroupId, student.Id))
                .ConfigureAwait(false);

            if (result.IsFailure)
            {
                ShowFailure(result.Failure!);
                return;
            }

            SelectedAvailable = null;
            if (result.Value.AlreadyEnrolled)
            {
                ShowSuccess(TranslationKeys.Enrollment.AlreadyEnrolled);
            }
            else
            {
                ShowSuccess(
                    Group.IsFull
                        ? TranslationKeys.Enrollment.CapacityExceededWarning
                        : TranslationKeys.Enrollment.EnrolledSuccess);
            }

            await LoadAsync().ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task WithdrawAsync(EnrollmentListItem? item)
    {
        var enrollment = item ?? SelectedEnrolled;
        if (enrollment is null)
        {
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            _localization.Tr(TranslationKeys.Enrollment.Title),
            _localization.Tr(
                TranslationKeys.Enrollment.UnenrollConfirm,
                ("name", enrollment.StudentName)),
            _localization.Tr(TranslationKeys.Enrollment.Unenroll),
            _localization.Tr(TranslationKeys.Common.Cancel)).ConfigureAwait(false);

        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _withdraw.ExecuteAsync(enrollment.Id).ConfigureAwait(false);
            if (result.IsFailure)
            {
                ShowFailure(result.Failure!);
                return;
            }

            SelectedEnrolled = null;
            ShowSuccess(TranslationKeys.Enrollment.UnenrolledSuccess);
            await LoadAsync().ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RenewLicenseAsync()
    {
        await _license.RenewInteractiveAsync().ConfigureAwait(false);
        await LoadAsync().ConfigureAwait(false);
    }

    partial void OnSearchTextChanged(string? value) => _ = RefreshIfLoadedAsync();

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    partial void OnGroupChanged(GroupSummary? value) => RaiseBoardProperties();

    private async Task RefreshIfLoadedAsync()
    {
        if (!_isLoaded)
        {
            return;
        }

        await LoadAsync().ConfigureAwait(false);
    }

    private void RaiseBoardProperties()
    {
        OnPropertyChanged(nameof(GroupName));
        OnPropertyChanged(nameof(SubjectText));
        OnPropertyChanged(nameof(TeacherText));
        OnPropertyChanged(nameof(IsFull));
        OnPropertyChanged(nameof(CapacityText));
        OnPropertyChanged(nameof(CapacityPercent));
        OnPropertyChanged(nameof(HasAvailable));
        OnPropertyChanged(nameof(HasEnrolled));
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

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Title));
        RaiseBoardProperties();
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
