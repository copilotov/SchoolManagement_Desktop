using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Localization;
using SchoolManagement.Domain.Licensing;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>
/// One instance for the whole shell: the status bar banner, the settings page
/// and the focus refresh all read the same <see cref="Info"/>. It never touches
/// the database or the file system, only <see cref="ILicenseService"/>.
/// </summary>
public partial class LicenseViewModel : ObservableObject, IDisposable
{
    private readonly ILicenseService _licenseService;
    private readonly IActivationDialogService _activationDialog;
    private readonly ILocalizationService _localization;

    [ObservableProperty]
    private LicenseInfo _info = LicenseInfo.Unlicensed(string.Empty);

    /// <summary>
    /// A warning the user has already acknowledged. A read-only license is the
    /// only state that cannot be dismissed: the app is blocked, so hiding the
    /// explanation would leave the user without a way forward.
    /// </summary>
    private bool _isDismissed;

    public LicenseViewModel(
        ILicenseService licenseService,
        IActivationDialogService activationDialog,
        ILocalizationService localization)
    {
        _licenseService = licenseService;
        _activationDialog = activationDialog;
        _localization = localization;
        _localization.LanguageChanged += OnLanguageChanged;
    }

    public bool HasBanner => Info.ShouldWarn
        && !string.IsNullOrEmpty(BannerMessage)
        && !(CanDismiss && _isDismissed);

    /// <summary>Read-only is the only state that must stay visible.</summary>
    public bool CanDismiss => Info.Status != LicenseStatus.ReadOnly;

    public Visibility DismissVisibility => CanDismiss ? Visibility.Visible : Visibility.Collapsed;

    public string DismissText => _localization.Tr(TranslationKeys.License.Dismiss);

    public Visibility BannerVisibility => HasBanner ? Visibility.Visible : Visibility.Collapsed;

    public bool HasBannerTitle => !string.IsNullOrEmpty(BannerTitle);

    public Visibility BannerTitleVisibility => HasBannerTitle ? Visibility.Visible : Visibility.Collapsed;

    public string BannerTitle => Info.BannerTitleKey is { } key ? _localization.Tr(key) : string.Empty;

    public string BannerMessage =>
        Info.BannerMessageKey is { } key ? _localization.Tr(key, Info.BannerParameters) : string.Empty;

    /// <summary>"Info", "Warning" or "Danger": the view maps it to theme brushes.</summary>
    public string Severity => Info.ClockTampered || Info.Status == LicenseStatus.ReadOnly
        ? "Danger"
        : Info.Status == LicenseStatus.GracePeriod
            ? "Warning"
            : "Info";

    public string StatusText => _localization.Tr(Info.StatusMessageKey);

    public string RenewText => _localization.Tr(TranslationKeys.License.RenewNow);

    public string DaysRemainingText => _localization.Tr(
        TranslationKeys.License.DaysRemaining,
        ("days", Info.DaysRemaining.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    public Task RefreshAsync(CancellationToken cancellationToken = default) =>
        RefreshCoreAsync(cancellationToken);

    /// <summary>Applies the result of the startup bootstrap without a second load.</summary>
    public void Apply(LicenseInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        Info = info;
    }

    [RelayCommand]
    private async Task RenewAsync()
    {
        var activated = await _activationDialog.ShowAsync();
        if (activated)
        {
            await RefreshCoreAsync(CancellationToken.None);
        }
    }

    [RelayCommand]
    private void Dismiss()
    {
        if (!CanDismiss)
        {
            return;
        }

        _isDismissed = true;
        OnPropertyChanged(nameof(HasBanner));
        OnPropertyChanged(nameof(BannerVisibility));
    }

    /// <summary>
    /// Opens the activation window and refreshes the status. Feature screens use
    /// this so a blocked write always offers the same "Renew license" flow.
    /// </summary>
    public Task RenewInteractiveAsync() => RenewAsync();

    private async Task RefreshCoreAsync(CancellationToken cancellationToken)
    {
        var info = await _licenseService.LoadAndVerifyAsync(cancellationToken);
        Apply(info);
    }

    partial void OnInfoChanged(LicenseInfo value)
    {
        // A new state deserves a fresh look even if the previous warning was
        // dismissed.
        _isDismissed = false;

        OnPropertyChanged(nameof(HasBanner));
        OnPropertyChanged(nameof(BannerVisibility));
        OnPropertyChanged(nameof(CanDismiss));
        OnPropertyChanged(nameof(DismissVisibility));
        OnPropertyChanged(nameof(HasBannerTitle));
        OnPropertyChanged(nameof(BannerTitleVisibility));
        OnPropertyChanged(nameof(BannerTitle));
        OnPropertyChanged(nameof(BannerMessage));
        OnPropertyChanged(nameof(Severity));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(DaysRemainingText));
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(BannerTitle));
        OnPropertyChanged(nameof(BannerMessage));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(RenewText));
        OnPropertyChanged(nameof(DismissText));
        OnPropertyChanged(nameof(DaysRemainingText));
        OnPropertyChanged(nameof(HasBanner));
        OnPropertyChanged(nameof(BannerVisibility));
        OnPropertyChanged(nameof(CanDismiss));
        OnPropertyChanged(nameof(DismissVisibility));
    }

    public void Dispose()
    {
        _localization.LanguageChanged -= OnLanguageChanged;
    }
}

/// <summary>
/// Small MVVM seam: the ViewModel asks for the activation screen without
/// knowing how to create a WPF window.
/// </summary>
public interface IActivationDialogService
{
    Task<bool> ShowAsync();
}
