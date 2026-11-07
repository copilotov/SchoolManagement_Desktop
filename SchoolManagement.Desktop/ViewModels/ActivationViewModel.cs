using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Localization;
using SchoolManagement.Domain.Licensing;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>
/// Activation screen: shows the full fingerprint and its short code, accepts a
/// license, and surfaces the exact structured failure reason. It is shared by
/// the first-run window and the "Renew license" dialog.
/// </summary>
public partial class ActivationViewModel : ObservableObject, IDisposable
{
    private readonly ILicenseService _licenseService;
    private readonly ILocalizationService _localization;
    private DeviceFingerprint _fingerprint = new(string.Empty, false, false);

    [ObservableProperty]
    private string _fingerprintHash = string.Empty;

    [ObservableProperty]
    private string _shortCode = string.Empty;

    [ObservableProperty]
    private bool _isFingerprintWeak;

    [ObservableProperty]
    private string _licenseText = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _noticeMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public ActivationViewModel(ILicenseService licenseService, ILocalizationService localization)
    {
        _licenseService = licenseService;
        _localization = localization;
        _localization.LanguageChanged += OnLanguageChanged;
    }

    public event EventHandler? Activated;

    public FlowDirection FlowDirection => _localization.IsRightToLeft
        ? FlowDirection.RightToLeft
        : FlowDirection.LeftToRight;

    public Visibility WeakFingerprintVisibility =>
        IsFingerprintWeak ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ErrorVisibility =>
        string.IsNullOrEmpty(ErrorMessage) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility NoticeVisibility =>
        string.IsNullOrEmpty(NoticeMessage) ? Visibility.Collapsed : Visibility.Visible;

    public bool IsNotBusy => !IsBusy;

    public string ActivateButtonText => _localization.Tr(
        IsBusy ? TranslationKeys.License.Activating : TranslationKeys.License.Activate);

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _fingerprint = await _licenseService.GetDeviceFingerprintAsync(cancellationToken);
        FingerprintHash = _fingerprint.Hash;
        ShortCode = _fingerprint.ShortCode;
        IsFingerprintWeak = !_fingerprint.IsStrong;
    }

    [RelayCommand]
    private void CopyFingerprint()
    {
        try
        {
            Clipboard.SetText(FingerprintHash);
            ErrorMessage = string.Empty;
            NoticeMessage = _localization.Tr(TranslationKeys.License.FingerprintCopied);
        }
        catch (Exception)
        {
            ErrorMessage = _localization.Tr(TranslationKeys.Errors.Unexpected);
        }
    }

    [RelayCommand]
    private void Paste()
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                LicenseText = Clipboard.GetText().Trim();
            }
        }
        catch (Exception)
        {
            ErrorMessage = _localization.Tr(TranslationKeys.Errors.Unexpected);
        }
    }

    [RelayCommand]
    private async Task ActivateAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = string.Empty;
        NoticeMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(LicenseText))
        {
            ErrorMessage = _localization.Tr(TranslationKeys.License.EnterKey);
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _licenseService.ActivateAsync(LicenseText);
            if (result.IsFailure)
            {
                // Exact structured reason, localised through the shared mapper.
                ErrorMessage = result.Failure!.ToDisplayMessage(_localization);
                return;
            }

            NoticeMessage = _localization.Tr(TranslationKeys.License.Activated);
            Activated?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnIsFingerprintWeakChanged(bool value) =>
        OnPropertyChanged(nameof(WeakFingerprintVisibility));

    partial void OnErrorMessageChanged(string value) =>
        OnPropertyChanged(nameof(ErrorVisibility));

    partial void OnNoticeMessageChanged(string value) =>
        OnPropertyChanged(nameof(NoticeVisibility));

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotBusy));
        OnPropertyChanged(nameof(ActivateButtonText));
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ActivateButtonText));
        OnPropertyChanged(nameof(FlowDirection));
    }

    public void Dispose()
    {
        _localization.LanguageChanged -= OnLanguageChanged;
        Activated = null;
    }
}
