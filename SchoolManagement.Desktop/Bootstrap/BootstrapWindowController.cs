using SchoolManagement.Desktop.Views;

namespace SchoolManagement.Desktop.Bootstrap;

/// <summary>WPF adapter for IBootstrapView. The gate remains testable without windows.</summary>
public sealed class BootstrapWindowController : IBootstrapView
{
    private readonly BootstrapWindow _window;

    public BootstrapWindowController(BootstrapWindow window)
    {
        _window = window;
        _window.RetryRequested += (_, _) => RetryRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? RetryRequested;

    public void ShowSplash() => _window.ShowSplash();

    public void ShowError() => _window.ShowError();

    public void Close() => _window.Close();
}
