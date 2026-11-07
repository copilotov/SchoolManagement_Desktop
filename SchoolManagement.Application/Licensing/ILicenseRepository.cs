using SchoolManagement.Application.Common;
using SchoolManagement.Domain.Licensing;

namespace SchoolManagement.Application.Licensing;

public interface ILicenseRepository
{
    Task<DeviceFingerprint> GetDeviceFingerprintAsync(CancellationToken cancellationToken = default);
    Task<LicenseInfo> LoadAndVerifyAsync(CancellationToken cancellationToken = default);
    Task<Result<LicenseInfo>> ActivateAsync(string licenseText, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
