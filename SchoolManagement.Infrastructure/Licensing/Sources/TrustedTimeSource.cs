#if WINDOWS
using System.Security.Cryptography;
#endif
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Paths;

namespace SchoolManagement.Infrastructure.Licensing.Sources;

/// <summary>
/// DPAPI-encrypted "highest observed instant". Storing it encrypted stops a
/// casual user from editing the file to bypass the clock rollback check.
/// </summary>
public sealed class TrustedTimeSource : ITrustedTimeStorage
{
    private readonly string _path;
    private readonly ILogger<TrustedTimeSource> _logger;

    public TrustedTimeSource(AppPaths paths, ILogger<TrustedTimeSource> logger)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _path = System.IO.Path.Combine(paths.DataDirectory, "trusted_time.dat");
        _logger = logger;
    }

    public TrustedTimeRecord? Read()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            var encrypted = File.ReadAllBytes(_path);
#if WINDOWS
            var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
#else
            var decrypted = encrypted;
#endif
            var json = Encoding.UTF8.GetString(decrypted);
            var dto = JsonSerializer.Deserialize<TrustedTimeDto>(json);
            if (dto is null ||
                !DateTime.TryParse(
                    dto.LastKnown,
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var lastKnown) ||
                !DateTime.TryParse(
                    dto.ObservedAt,
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var observed))
            {
                return null;
            }

            return new TrustedTimeRecord(
                lastKnown.ToUniversalTime(),
                observed.ToUniversalTime(),
                dto.Rollback);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Failed to read the trusted time file");
            return null;
        }
    }

    public void Write(TrustedTimeRecord record)
    {
        try
        {
            var dto = new TrustedTimeDto
            {
                LastKnown = record.LastKnownUtc.ToUniversalTime().ToString("o"),
                ObservedAt = record.ObservedAtUtc.ToUniversalTime().ToString("o"),
                Rollback = record.NoticedRollback,
            };

            var json = JsonSerializer.Serialize(dto);
            var bytes = Encoding.UTF8.GetBytes(json);
#if WINDOWS
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
#else
            var encrypted = bytes;
#endif
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
            File.WriteAllBytes(_path, encrypted);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Failed to write the trusted time file");
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Failed to clear the trusted time file");
        }
    }

    private sealed class TrustedTimeDto
    {
        public string LastKnown { get; set; } = string.Empty;

        public string ObservedAt { get; set; } = string.Empty;

        public bool Rollback { get; set; }
    }
}
