namespace SchoolManagement.Desktop.Services;

/// <summary>
/// Small MVVM seam for modal user interaction. ViewModels pass localized text,
/// so the implementation never needs the localization service and the
/// ViewModels stay testable without a WPF message loop.
/// </summary>
public interface IUserDialogService
{
    Task<bool> ConfirmAsync(
        string title,
        string message,
        string confirmText,
        string cancelText);

    Task ShowInfoAsync(string title, string message, string okText);
}
