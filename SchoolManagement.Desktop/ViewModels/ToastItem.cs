using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>
/// One visible toast. The text is already localized by the caller; the severity
/// only selects a palette token in the view. Closing is delegated to the service
/// so the collection stays the single source of truth.
/// </summary>
public partial class ToastItem : ObservableObject
{
    private readonly Action<ToastItem> _close;

    public ToastItem(string message, ToastSeverity severity, Action<ToastItem> close)
    {
        Message = message;
        Severity = severity;
        _close = close;
    }

    public string Message { get; }

    public ToastSeverity Severity { get; }

    /// <summary>Invoked by the lifetime timer as well as by the close button.</summary>
    public void RequestClose() => _close(this);

    [RelayCommand]
    private void Close() => _close(this);
}
