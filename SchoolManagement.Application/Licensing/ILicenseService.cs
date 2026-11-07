using SchoolManagement.Application.Common;
using SchoolManagement.Domain.Licensing;

namespace SchoolManagement.Application.Licensing;

public sealed record LicenseActivationResult(LicenseInfo? Info, LicenseFailureReason? FailureReason);

public interface ILicenseService
{
    Task<DeviceFingerprint> GetDeviceFingerprintAsync(CancellationToken cancellationToken = default);
    Task<LicenseInfo> LoadAndVerifyAsync(CancellationToken cancellationToken = default);
    Task<Result<LicenseInfo>> ActivateAsync(string licenseText, CancellationToken cancellationToken = default);
    Task DeactivateAsync(CancellationToken cancellationToken = default);
}
