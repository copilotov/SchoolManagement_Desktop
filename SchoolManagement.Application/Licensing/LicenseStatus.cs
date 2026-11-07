namespace SchoolManagement.Application.Licensing;

/// <summary>
/// Bootstraps the license before the shell exists. Returns data, never throws
/// for an expected failure (missing/invalid license).
/// </summary>
public interface ILicenseBootstrapper
{
    Task<LicenseInfo> InitializeAsync(CancellationToken cancellationToken = default);
}
