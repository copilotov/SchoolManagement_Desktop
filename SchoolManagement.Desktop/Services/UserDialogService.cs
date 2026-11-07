using SchoolManagement.Desktop.Views;

namespace SchoolManagement.Desktop.Services;

/// <summary>
/// Creates the shared confirmation window on demand. The window is transient:
/// each call gets a fresh instance, and closing it returns the answer to the
/// caller instead of mutating global state.
/// </summary>
public sealed class UserDialogService : IUserDialogService
{
    public Task<bool> ConfirmAsync(
        string title,
        string message,
        string confirmText,
        string cancelText)
    {
        var window = new ConfirmationWindow();
        var confirmed = window.Configure(title, message, confirmText, cancelText) == true;
        return Task.FromResult(confirmed);
    }

    public Task ShowInfoAsync(string title, string message, string okText)
    {
        var window = new ConfirmationWindow();
        window.Configure(title, message, okText, cancelText: null);
        return Task.CompletedTask;
    }
}
