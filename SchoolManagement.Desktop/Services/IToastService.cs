namespace SchoolManagement.Desktop.Services;

/// <summary>Visual weight of an in-app toast.</summary>
public enum ToastSeverity
{
    Info = 0,
    Success = 1,
    Warning = 2,
    Danger = 3,
}

/// <summary>
/// Phase D2 - non-blocking in-app notifications. A toast never interrupts the
/// user, never steals focus and never covers a modal: it appears in the shell's
/// toast host and disappears on its own. ViewModels pass already-localized text,
/// so this service has no localization dependency and stays testable.
/// </summary>
public interface IToastService
{
    /// <summary>Newest first. Bound by <c>ToastHostView</c>.</summary>
    System.Collections.ObjectModel.ReadOnlyObservableCollection<ViewModels.ToastItem> Items { get; }

    void Show(string message, ToastSeverity severity = ToastSeverity.Info);

    void ShowSuccess(string message);

    void ShowWarning(string message);

    void ShowError(string message);

    void Dismiss(ViewModels.ToastItem item);
}
