#if WINDOWS
using System.Security.Cryptography;
#endif
using System.Text;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Paths;

namespace SchoolManagement.Infrastructure.Licensing.Sources;

/// <summary>
/// DPAPI-encrypted license file. The scope is <see cref="DataProtectionScope.CurrentUser"/>
/// on purpose: a school PC is used by one Windows account, and
/// <c>LocalMachine</c> would let any local account read or copy the license.
/// The encrypted blob can be copied, but only the same user on the same machine
/// can decrypt it, which is exactly the intent.
///
/// A missing or unreadable file is treated as "no license"; a present but
/// invalid license is never deleted by <see cref="Read"/>.
/// </summary>
public sealed class LicenseLocalSource : ILicenseStorage
{
    private readonly string _path;
    private readonly ILogger<LicenseLocalSource> _logger;

    public LicenseLocalSource(AppPaths paths, ILogger<LicenseLocalSource> logger)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _path = System.IO.Path.Combine(paths.DataDirectory, "license.dat");
        _logger = logger;
    }

    public string? Read()
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
            return Encoding.UTF8.GetString(decrypted);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Failed to read the license file");
            return null;
        }
    }

    public void Write(string licenseText)
    {
        try
        {
            var bytes = Encoding.UTF8.GetBytes(licenseText.Trim());
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
            _logger.LogDebug(exception, "Failed to write the license file");
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
            _logger.LogDebug(exception, "Failed to clear the license file");
        }
    }
}
