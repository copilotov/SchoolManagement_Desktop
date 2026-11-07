using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Desktop.Services;

/// <summary>
/// Atomic <c>window.json</c> store. Like the settings store, a missing or
/// corrupt file falls back to the default placement and never crashes startup.
/// </summary>
public sealed class WindowStateStore : IWindowStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    private readonly string _path;
    private readonly ILogger<WindowStateStore> _logger;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public WindowStateStore(string path, ILogger<WindowStateStore> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(logger);

        _path = path;
        _logger = logger;
    }

    /// <summary><c>%LOCALAPPDATA%\SchoolManagement\window.json</c>.</summary>
    public static string DefaultPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = AppContext.BaseDirectory;
        }

        return Path.Combine(localAppData, "SchoolManagement", "window.json");
    }

    public async Task<WindowPlacement> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            return WindowPlacement.Default;
        }

        try
        {
            await using var stream = File.OpenRead(_path);
            var placement = await JsonSerializer
                .DeserializeAsync<WindowPlacement>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            return placement ?? WindowPlacement.Default;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not read the window state file '{Path}'.", _path);
            return WindowPlacement.Default;
        }
    }

    public async Task<bool> SaveAsync(WindowPlacement placement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(placement);

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = $"{_path}.tmp";
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer
                    .SerializeAsync(stream, placement, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
            }

            File.Move(temporaryPath, _path, overwrite: true);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not save the window state file '{Path}'.", _path);
            return false;
        }
        finally
        {
            _writeGate.Release();
        }
    }
}
