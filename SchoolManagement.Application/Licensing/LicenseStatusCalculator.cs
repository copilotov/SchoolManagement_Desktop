using SchoolManagement.Domain.Licensing;

namespace SchoolManagement.Application.Licensing;

/// <summary>
/// Pure state machine for the seven license states. It has no clock, no file
/// and no WPF: the caller passes the trusted "now".
///
/// Boundaries (all dates inclusive):
/// <list type="bullet">
/// <item><c>nbf</c> day itself is not "not started"; it behaves as active;</item>
/// <item>expiry day and the 30 days before it are <c>ExpiringSoon</c>;</item>
/// <item>the 30 days after expiry are <c>GracePeriod</c> and writing is allowed;</item>
/// <item>the day after that is <c>ReadOnly</c>.</item>
/// </list>
/// </summary>
public static class LicenseStatusCalculator
{
    public const int WarningDays = 30;
    public const int GraceDays = 30;

    public static LicenseInfo Calculate(
        LicensePayload payload,
        string rawLicense,
        DateTime nowUtc,
        bool clockTampered,
        bool fingerprintIsStrong = true)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var nowDate = DateOnly.FromDateTime(nowUtc);
        var expiryDate = payload.ExpiryDate;
        var startsOn = payload.NotBefore;

        if (startsOn is { } notBefore && nowDate < notBefore)
        {
            return new LicenseInfo(
                LicenseStatus.NotStarted,
                payload.Fingerprint,
                expiryDate,
                notBefore.DayNumber - nowDate.DayNumber,
                payload.LicensedTo,
                rawLicense,
                null,
                clockTampered,
                null,
                null,
                notBefore,
                fingerprintIsStrong);
        }

        if (nowDate <= expiryDate)
        {
            var daysRemaining = expiryDate.DayNumber - nowDate.DayNumber;
            var status = daysRemaining <= WarningDays
                ? LicenseStatus.ExpiringSoon
                : LicenseStatus.Active;

            return new LicenseInfo(
                status,
                payload.Fingerprint,
                expiryDate,
                daysRemaining,
                payload.LicensedTo,
                rawLicense,
                null,
                clockTampered,
                null,
                null,
                startsOn,
                fingerprintIsStrong);
        }

        var daysPastExpiry = nowDate.DayNumber - expiryDate.DayNumber;
        var readOnlyStartsOn = expiryDate.AddDays(GraceDays + 1);

        if (daysPastExpiry <= GraceDays)
        {
            // First grace day: 30 left. Last grace day: 1 left. Honest countdown.
            var graceDaysLeft = GraceDays - daysPastExpiry + 1;
            return new LicenseInfo(
                LicenseStatus.GracePeriod,
                payload.Fingerprint,
                expiryDate,
                0,
                payload.LicensedTo,
                rawLicense,
                null,
                clockTampered,
                graceDaysLeft,
                readOnlyStartsOn,
                startsOn,
                fingerprintIsStrong);
        }

        return new LicenseInfo(
            LicenseStatus.ReadOnly,
            payload.Fingerprint,
            expiryDate,
            0,
            payload.LicensedTo,
            rawLicense,
            null,
            clockTampered,
            0,
            readOnlyStartsOn,
            startsOn,
            fingerprintIsStrong);
    }
}
