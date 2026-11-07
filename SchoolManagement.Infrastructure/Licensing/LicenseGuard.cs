using SchoolManagement.Application.Common;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Localization;

namespace SchoolManagement.Infrastructure.Licensing;

/// <summary>
/// The single write gate. The repository call is synchronous here because
/// every write use case is synchronous at the guard boundary; all awaits inside
/// the repository use <c>ConfigureAwait(false)</c> so there is no deadlock.
/// </summary>
public sealed class LicenseGuard : ILicenseGuard
{
    private readonly ILicenseService _licenseService;

    public LicenseGuard(ILicenseService licenseService)
    {
        _licenseService = licenseService;
    }

    public Result<bool> EnsureCanWrite()
    {
        var info = _licenseService.LoadAndVerifyAsync().GetAwaiter().GetResult();

        if (info.CanWrite)
        {
            return Result<bool>.Success(true);
        }

        return Result<bool>.Fail(Failure.License(
            TranslationKeys.License.ErrorWriteBlocked,
            info.FailureReason?.ToString() ?? info.Status.ToString()));
    }
}
