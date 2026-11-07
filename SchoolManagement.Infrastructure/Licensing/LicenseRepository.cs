using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Localization;
using SchoolManagement.Domain.Licensing;
using SchoolManagement.Infrastructure.Licensing.Ed25519;

namespace SchoolManagement.Infrastructure.Licensing;

/// <summary>
/// The real activation flow: fingerprint, encrypted storage, signature
/// verification, trusted clock and status calculation. Only the fingerprint
/// source and the two storage ports are replaceable; everything else is the
/// production code path exercised by the tests.
/// </summary>
public sealed class LicenseRepository : ILicenseRepository
{
    private readonly IDeviceFingerprintSource _fingerprintSource;
    private readonly ILicenseStorage _storage;
    private readonly LicenseVerifier _verifier;
    private readonly TrustedClockService _trustedClock;
    private readonly ILogger<LicenseRepository> _logger;

    private DeviceFingerprint? _cachedFingerprint;

    public LicenseRepository(
        IDeviceFingerprintSource fingerprintSource,
        ILicenseStorage storage,
        LicenseVerifier verifier,
        TrustedClockService trustedClock,
        ILogger<LicenseRepository> logger)
    {
        _fingerprintSource = fingerprintSource;
        _storage = storage;
        _verifier = verifier;
        _trustedClock = trustedClock;
        _logger = logger;
    }

    public async Task<DeviceFingerprint> GetDeviceFingerprintAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedFingerprint is not null)
        {
            return _cachedFingerprint;
        }

        _cachedFingerprint = await _fingerprintSource.GetFingerprintAsync(cancellationToken).ConfigureAwait(false);
        return _cachedFingerprint;
    }

    public async Task<LicenseInfo> LoadAndVerifyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var fingerprint = await GetDeviceFingerprintAsync(cancellationToken).ConfigureAwait(false);
            var licenseText = _storage.Read();

            if (string.IsNullOrWhiteSpace(licenseText))
            {
                return LicenseInfo.Unlicensed(fingerprint.Hash, fingerprint.IsStrong);
            }

            var result = _verifier.Verify(licenseText, fingerprint.Hash);
            if (!result.IsValid || result.Payload is null)
            {
                var reason = result.FailureReason ?? LicenseFailureReason.Malformed;
                _logger.LogDebug("License is invalid: {Reason}", reason);
                return LicenseInfo.Invalid(reason, fingerprint.Hash, licenseText, fingerprint.IsStrong);
            }

            return BuildInfo(result.Payload, licenseText, fingerprint);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // A repository never leaks an exception to the UI.
            _logger.LogWarning(exception, "Failed to load the license");
            return LicenseInfo.Invalid(LicenseFailureReason.Malformed, string.Empty);
        }
    }

    public async Task<Result<LicenseInfo>> ActivateAsync(
        string licenseText,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var trimmed = licenseText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return Result<LicenseInfo>.Fail(
                    Failure.Validation(TranslationKeys.License.EnterKey));
            }

            var fingerprint = await GetDeviceFingerprintAsync(cancellationToken).ConfigureAwait(false);
            var result = _verifier.Verify(trimmed, fingerprint.Hash);
            if (!result.IsValid || result.Payload is null)
            {
                var reason = result.FailureReason ?? LicenseFailureReason.Malformed;
                _logger.LogInformation("Activation rejected: {Reason}", reason);

                // Deliberately do not touch storage: an invalid license may be
                // valid for another device, so it is neither saved nor deleted.
                return Result<LicenseInfo>.Fail(
                    Failure.License(LicenseFailureMapper.ToTranslationKey(reason), reason.ToString()));
            }

            // Anchor the trusted clock before the first read, then persist the
            // license only once it is proven authentic.
            _trustedClock.SetAnchor(result.Payload.IssuedAt);
            _storage.Write(trimmed);

            var info = BuildInfo(result.Payload, trimmed, fingerprint);
            return Result<LicenseInfo>.Success(info);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to activate the license");
            return Result<LicenseInfo>.Fail(Failure.License(
                LicenseFailureMapper.ToTranslationKey(LicenseFailureReason.Malformed),
                exception.Message));
        }
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _storage.Clear();
        return Task.CompletedTask;
    }

    private LicenseInfo BuildInfo(LicensePayload payload, string licenseText, DeviceFingerprint fingerprint)
    {
        _trustedClock.SetAnchor(payload.IssuedAt);
        var verdict = _trustedClock.EvaluateAndRecord();

        return LicenseStatusCalculator.Calculate(
            payload,
            licenseText,
            verdict.TrustedUtc,
            verdict.ClockTampered,
            fingerprint.IsStrong);
    }
}
