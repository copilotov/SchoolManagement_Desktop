using SchoolManagement.Domain.Licensing;

namespace SchoolManagement.Application.Licensing;

/// <summary>Where the activated license text lives (DPAPI file in production).</summary>
public interface ILicenseStorage
{
    string? Read();

    void Write(string licenseText);

    void Clear();
}

/// <summary>
/// The highest instant ever observed on this device. Persisted encrypted so a
/// user cannot extend a license by moving the system clock back.
/// </summary>
public sealed record TrustedTimeRecord(
    DateTime LastKnownUtc,
    DateTime ObservedAtUtc,
    bool NoticedRollback);

public interface ITrustedTimeStorage
{
    TrustedTimeRecord? Read();

    void Write(TrustedTimeRecord record);

    void Clear();
}

/// <summary>
/// Replacement seam for the Windows fingerprint (registry + WMI). Tests use a
/// fake; the desktop uses <c>WindowsDeviceFingerprintSource</c>.
/// </summary>
public interface IDeviceFingerprintSource
{
    Task<DeviceFingerprint> GetFingerprintAsync(CancellationToken cancellationToken = default);
}
