using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Licensing;

namespace SchoolManagement.Infrastructure.Licensing;

/// <summary>
/// Runs the license step of the startup order and returns data. It never
/// throws for a missing or invalid license: those are states, not crashes.
/// </summary>
public sealed class LicenseBootstrapper : ILicenseBootstrapper
{
    private readonly ILicenseRepository _repository;
    private readonly ILogger<LicenseBootstrapper> _logger;

    public LicenseBootstrapper(ILicenseRepository repository, ILogger<LicenseBootstrapper> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<LicenseInfo> InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var info = await _repository.LoadAndVerifyAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "License status: {Status}; clock tampered: {ClockTampered}; reason: {Reason}",
            info.Status,
            info.ClockTampered,
            info.FailureReason);

        return info;
    }
}
