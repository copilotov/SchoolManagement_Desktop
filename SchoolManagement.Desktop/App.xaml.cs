using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.DependencyInjection;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Paths;
using SchoolManagement.Application.Persistence;
using SchoolManagement.Application.Settings;
using SchoolManagement.Desktop.Bootstrap;
using SchoolManagement.Desktop.DependencyInjection;
using SchoolManagement.Desktop.Localization;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.ViewModels;
using SchoolManagement.Desktop.Views;
using SchoolManagement.Infrastructure.DependencyInjection;
using SchoolManagement.Infrastructure.Localization;
using SchoolManagement.Infrastructure.Settings;
using WpfApplication = System.Windows.Application;

namespace SchoolManagement.Desktop;

/// <summary>
/// Composition root. Startup is deliberately ordered:
/// splash -> settings/translation -> DI host -> database -> license -> shell.
/// No screen, repository or use case is created before the splash is visible.
/// </summary>
public partial class App : WpfApplication
{
    private IHost? _host;
    private ILoggerFactory? _loggerFactory;
    private BootstrapWindowController? _bootstrapController;
    private BootstrapGate? _bootstrapGate;
    private ShellViewModel? _shellViewModel;
    private LicenseViewModel? _licenseViewModel;
    private LicenseInfo? _startupLicense;
    private AppPaths? _paths;
    private AppSettings _settings = new();
    private bool _isRetrying;
    private bool _isShowingActivation;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _paths = AppPaths.CreateDefault();
        Directory.CreateDirectory(_paths.DataDirectory);
        Directory.CreateDirectory(_paths.LogsDirectory);

        _loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Information));

        var bootstrapWindow = new BootstrapWindow();
        bootstrapWindow.Closed += OnBootstrapWindowClosed;
        _bootstrapController = new BootstrapWindowController(bootstrapWindow);
        _bootstrapController.RetryRequested += OnRetryRequested;

        _bootstrapGate = new BootstrapGate(
            _bootstrapController,
            InitializeAsync,
            _loggerFactory.CreateLogger<BootstrapGate>());

        await RunBootstrapAsync();
    }

    private async Task RunBootstrapAsync()
    {
        if (_bootstrapGate is null)
        {
            return;
        }

        var succeeded = await _bootstrapGate.RunAsync();
        if (!succeeded)
        {
            return;
        }

        try
        {
            await ShowMainWindowAsync();
        }
        catch (Exception exception)
        {
            WriteFatalLog(exception);
            _bootstrapController?.ShowError();
        }
    }

    private void OnBootstrapWindowClosed(object? sender, EventArgs e)
    {
        _bootstrapController = null;

        // Alt+F4 on the error screen must exit instead of leaving a hidden
        // process alive under OnExplicitShutdown. While the activation window is
        // the only screen, closing the splash is intentional, not an exit.
        if (MainWindow is null && !_isShowingActivation)
        {
            Shutdown();
        }
    }

    private async void OnRetryRequested(object? sender, EventArgs e)
    {
        if (_isRetrying)
        {
            return;
        }

        _isRetrying = true;
        try
        {
            await RunBootstrapAsync();
        }
        finally
        {
            _isRetrying = false;
        }
    }

    /// <summary>Runs on a background thread (see BootstrapGate).</summary>
    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await InitializeCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            WriteFatalLog(exception);
            throw;
        }
    }

    private async Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        if (_paths is null || _loggerFactory is null)
        {
            throw new InvalidOperationException("The application paths are not initialized.");
        }

        await DisposeHostAsync().ConfigureAwait(false);

        // 1. Preferences are read before translation so the saved language is used.
        var settingsStore = new JsonAppSettingsStore(
            _paths.SettingsFile,
            _loggerFactory.CreateLogger<JsonAppSettingsStore>());

        _settings = await settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false);

        // 2. Translation must succeed before any localized screen can be built.
        var localization = new LocalizationService(
            _paths.TranslationsDirectory,
            _loggerFactory.CreateLogger<LocalizationService>());

        var loaded = await localization.InitializeAsync(_settings.Language, cancellationToken).ConfigureAwait(false);
        if (!loaded)
        {
            throw new InvalidOperationException("The translation files could not be loaded.");
        }

        LocalizationProvider.Initialize(localization);

        // 3. Generic Host + DI. Infrastructure owns the database and license hooks.
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Information));
        builder.ConfigureServices(services =>
        {
            services.AddApplication();
            services.AddInfrastructure(_paths, localization, settingsStore);
            services.AddDesktop();
        });

        _host = builder.Build();
        await _host.StartAsync(cancellationToken).ConfigureAwait(false);

        // 4. Database must be usable before any repository is exposed.
        var database = _host.Services.GetRequiredService<IDatabaseInitializer>();
        await database.InitializeAsync(cancellationToken).ConfigureAwait(false);

        // 5. License bootstrap. The result decides the first screen: only a
        //    missing/invalid license goes to the activation window.
        var licensing = _host.Services.GetRequiredService<ILicenseBootstrapper>();
        _startupLicense = await licensing.InitializeAsync(cancellationToken).ConfigureAwait(false);

        // 6. Theme is applied before the shell is created, without restart.
        var theme = _host.Services.GetRequiredService<IThemeService>();
        theme.Apply(_settings.Theme);
    }

    private async Task ShowMainWindowAsync()
    {
        if (_host is null)
        {
            throw new InvalidOperationException("The dependency injection host is not ready.");
        }

        var license = _host.Services.GetRequiredService<LicenseViewModel>();
        _licenseViewModel = license;

        // Reuse the bootstrap result: no second fingerprint/WMI read and no
        // second trusted-clock write before the first window exists.
        license.Apply(_startupLicense ?? LicenseInfo.Unlicensed(string.Empty));

        if (license.Info.RequiresActivation)
        {
            // The splash is Topmost; close it before the modal activation
            // window so the activation UI is actually visible.
            _isShowingActivation = true;
            try
            {
                _bootstrapController?.Close();
                _bootstrapController = null;

                var dialog = _host.Services.GetRequiredService<IActivationDialogService>();
                var activated = await dialog.ShowAsync();
                if (!activated)
                {
                    // Policy: warn and go read-only, but never run unlicensed.
                    Shutdown();
                    return;
                }
            }
            finally
            {
                _isShowingActivation = false;
            }

            await license.RefreshAsync();
        }

        var shellWindow = _host.Services.GetRequiredService<ShellWindow>();
        _shellViewModel = shellWindow.DataContext as ShellViewModel;

        // Phase D2 - restore the saved geometry before the first frame so the
        // shell never appears at the default size and then jumps.
        await shellWindow.InitializePlacementAsync();

        MainWindow = shellWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        shellWindow.Show();

        _bootstrapController?.Close();
        _bootstrapController = null;
    }

    private async Task DisposeHostAsync()
    {
        if (_host is null)
        {
            return;
        }

        var host = _host;
        _host = null;

        try
        {
            await host.StopAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
        catch
        {
            // A failed stop must not block a retry.
        }
        finally
        {
            host.Dispose();
        }
    }

    private void WriteFatalLog(Exception exception)
    {
        try
        {
            if (_paths is null)
            {
                return;
            }

            Directory.CreateDirectory(_paths.LogsDirectory);
            var path = Path.Combine(_paths.LogsDirectory, "bootstrap.log");
            var line = $"{DateTimeOffset.UtcNow:O} {exception}{Environment.NewLine}{Environment.NewLine}";
            File.AppendAllText(path, line);
        }
        catch
        {
            // Diagnostics must never mask the original failure.
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _shellViewModel?.Dispose();
        _licenseViewModel?.Dispose();

        if (_bootstrapController is not null)
        {
            _bootstrapController.RetryRequested -= OnRetryRequested;
            _bootstrapController.Close();
            _bootstrapController = null;
        }

        try
        {
            DisposeHostAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // The process is exiting; nothing useful can be done here.
        }

        _loggerFactory?.Dispose();
        _loggerFactory = null;

        base.OnExit(e);
    }
}
