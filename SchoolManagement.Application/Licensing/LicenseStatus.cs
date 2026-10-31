namespace SchoolManagement.Application.Licensing;

public enum LicenseStatus
{
    Active,
    ExpiringSoon,
    GracePeriod,
    ReadOnly,
    NotStarted,
    Unlicensed,
    Invalid,
}

/// <summary>
/// Snapshot of the license state. The full Ed25519/DPAPI implementation is
/// scheduled for the licensing phase; Phase 1 only needs a bootstrap hook.
/// </summary>
public sealed record LicenseSnapshot(
    LicenseStatus Status,
    DateOnly? StartsOn = null,
    DateOnly? ExpiresOn = null,
    string? LicensedTo = null)
{
    public bool CanWrite => Status is LicenseStatus.Active
        or LicenseStatus.ExpiringSoon
        or LicenseStatus.GracePeriod;

    public bool ShouldWarn => Status is LicenseStatus.ExpiringSoon
        or LicenseStatus.GracePeriod
        or LicenseStatus.ReadOnly
        or LicenseStatus.NotStarted;
}

public interface ILicenseBootstrapper
{
    Task<LicenseSnapshot> InitializeAsync(CancellationToken cancellationToken = default);
}
