using Microsoft.Extensions.Logging;

namespace SchoolManagement.Desktop.Bootstrap;

/// <summary>
/// The splash is shown first; initialization runs on a background thread.
/// Failure shows an icon-only retry screen because translation itself may be
/// the thing that failed. This type has no WPF dependency and is unit tested.
/// </summary>
public interface IBootstrapView
{
    void ShowSplash();

    void ShowError();
}

public sealed class BootstrapGate
{
    private readonly IBootstrapView _view;
    private readonly Func<CancellationToken, Task> _initialize;
    private readonly ILogger<BootstrapGate> _logger;

    public BootstrapGate(
        IBootstrapView view,
        Func<CancellationToken, Task> initialize,
        ILogger<BootstrapGate> logger)
    {
        _view = view;
        _initialize = initialize;
        _logger = logger;
    }

    /// <summary>Returns true when initialization completed successfully.</summary>
    public async Task<bool> RunAsync(CancellationToken cancellationToken = default)
    {
        _view.ShowSplash();

        try
        {
            // Task.Run guarantees that file I/O, EF Core and license work never
            // block the UI dispatcher while the splash is visible.
            await Task.Run(() => _initialize(cancellationToken), cancellationToken).ConfigureAwait(true);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Bootstrap cancelled.");
            return false;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Bootstrap failed.");
            _view.ShowError();
            return false;
        }
    }
}
