using SchoolManagement.Application.Common;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Domain.Licensing;

namespace SchoolManagement.Infrastructure.Licensing;

public sealed class LicenseService : ILicenseService
{
    private readonly ILicenseRepository _repository;

    public LicenseService(ILicenseRepository repository)
    {
        _repository = repository;
    }

    public Task<DeviceFingerprint> GetDeviceFingerprintAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetDeviceFingerprintAsync(cancellationToken);
    }

    public Task<LicenseInfo> LoadAndVerifyAsync(CancellationToken cancellationToken = default)
    {
        return _repository.LoadAndVerifyAsync(cancellationToken);
    }

    public Task<Result<LicenseInfo>> ActivateAsync(string licenseText, CancellationToken cancellationToken = default)
    {
        return _repository.ActivateAsync(licenseText, cancellationToken);
    }

    public Task DeactivateAsync(CancellationToken cancellationToken = default)
    {
        return _repository.ClearAsync(cancellationToken);
    }
}
