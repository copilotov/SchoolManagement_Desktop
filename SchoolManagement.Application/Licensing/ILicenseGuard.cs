using SchoolManagement.Application.Common;

namespace SchoolManagement.Application.Licensing;

/// <summary>
/// The central write gate. Every write use case calls it first; the returned
/// failure carries the shared "read-only" translation key so the UI can show
/// the same message and a "Renew license" button everywhere.
///
/// It returns <see cref="Result{T}"/> instead of throwing so a blocked write is
/// an expected outcome, not an exception crossing the repository boundary.
/// </summary>
public interface ILicenseGuard
{
    Result<bool> EnsureCanWrite();
}
