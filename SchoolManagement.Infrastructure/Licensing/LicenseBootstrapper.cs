using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Licensing;

namespace SchoolManagement.Infrastructure.Licensing;

/// <summary>
/// Phase 1 bootstrap hook only. Ed25519 verification, DPAPI storage, device
/// fingerprint and the status calculator are implemented in the licensing
/// phase; the status bar already reserves the space for the banner.
/// </summary>
public sealed class LicenseBootstrapper : ILicenseBootstrapper
{
    private readonly ILogger<LicenseBootstrapper> _logger;

    public LicenseBootstrapper(ILogger<LicenseBootstrapper> logger)
    {
        _logger = logger;
    }

    public Task<LicenseSnapshot> InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation("Licensing is not implemented in Phase 1; reporting Unlicensed.");
        return Task.FromResult(new LicenseSnapshot(LicenseStatus.Unlicensed));
    }
}
