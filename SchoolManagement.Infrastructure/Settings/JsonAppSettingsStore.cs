using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Settings;

namespace SchoolManagement.Infrastructure.Settings;

/// <summary>
/// Atomic settings.json store. Failure to read or write is never allowed to
/// crash the application: defaults are returned and the error is logged.
/// </summary>
public sealed class JsonAppSettingsStore : IAppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;
    private readonly ILogger<JsonAppSettingsStore> _logger;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public JsonAppSettingsStore(string path, ILogger<JsonAppSettingsStore> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(logger);

        _path = path;
        _logger = logger;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            return new AppSettings();
        }

        try
        {
            await using var stream = File.OpenRead(_path);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            return Sanitize(settings ?? new AppSettings());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not read settings file '{Path}'; using defaults.", _path);
            return new AppSettings();
        }
    }

    public async Task<bool> SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

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
                await JsonSerializer.SerializeAsync(stream, Sanitize(settings), SerializerOptions, cancellationToken)
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
            _logger.LogError(exception, "Could not save settings file '{Path}'.", _path);
            return false;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private static AppSettings Sanitize(AppSettings settings)
    {
        var language = string.IsNullOrWhiteSpace(settings.Language)
            ? "ar"
            : settings.Language.Trim().ToLowerInvariant();

        if (language is not ("ar" or "en" or "fr"))
        {
            language = "ar";
        }

        var theme = Enum.IsDefined(settings.Theme) ? settings.Theme : AppTheme.Light;
        return new AppSettings(language, theme);
    }
}
