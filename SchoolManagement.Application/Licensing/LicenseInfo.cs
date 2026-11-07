using SchoolManagement.Application.Localization;
using SchoolManagement.Domain.Licensing;

namespace SchoolManagement.Application.Licensing;

/// <summary>
/// Everything the UI needs to render a license, as data. No WPF type, no
/// message text: only translation keys and parameter values.
/// </summary>
public sealed record LicenseInfo(
    LicenseStatus Status,
    string FingerprintHash,
    DateOnly? ExpiryDate = null,
    int DaysRemaining = 0,
    string? LicensedTo = null,
    string RawLicense = "",
    LicenseFailureReason? FailureReason = null,
    bool ClockTampered = false,
    int? GraceDaysLeft = null,
    DateOnly? ReadOnlyStartsOn = null,
    DateOnly? StartsOn = null,
    bool FingerprintIsStrong = true)
{
    public static LicenseInfo Unlicensed(string fingerprintHash, bool fingerprintIsStrong = true) =>
        new(LicenseStatus.Unlicensed, fingerprintHash, FingerprintIsStrong: fingerprintIsStrong);

    public static LicenseInfo Invalid(
        LicenseFailureReason reason,
        string fingerprintHash,
        string rawLicense = "",
        bool fingerprintIsStrong = true,
        bool clockTampered = false) =>
        new(
            LicenseStatus.Invalid,
            fingerprintHash,
            RawLicense: rawLicense,
            FailureReason: reason,
            ClockTampered: clockTampered,
            FingerprintIsStrong: fingerprintIsStrong);

    /// <summary>
    /// Whether writing is allowed. Used by the guard, never to decide which
    /// first screen to show (read-only and not-started still open the shell).
    /// </summary>
    public bool CanWrite => Status is LicenseStatus.Active
        or LicenseStatus.ExpiringSoon
        or LicenseStatus.GracePeriod;

    /// <summary>
    /// The first screen decision: only a missing or invalid license goes to the
    /// activation page. A read-only license still opens the application.
    /// </summary>
    public bool RequiresActivation => Status is LicenseStatus.Unlicensed or LicenseStatus.Invalid;

    public bool ShouldWarn => Status is LicenseStatus.ExpiringSoon
        or LicenseStatus.GracePeriod
        or LicenseStatus.ReadOnly
        or LicenseStatus.NotStarted
        || ClockTampered;

    public string StatusMessageKey => Status switch
    {
        LicenseStatus.Active => TranslationKeys.License.StatusActive,
        LicenseStatus.ExpiringSoon => TranslationKeys.License.StatusExpiringSoon,
        LicenseStatus.GracePeriod => TranslationKeys.License.StatusGracePeriod,
        LicenseStatus.ReadOnly => TranslationKeys.License.StatusReadOnly,
        LicenseStatus.NotStarted => TranslationKeys.License.StatusNotStarted,
        LicenseStatus.Unlicensed => TranslationKeys.License.StatusUnlicensed,
        LicenseStatus.Invalid => TranslationKeys.License.StatusInvalid,
        _ => TranslationKeys.License.StatusInvalid,
    };

    public string? FailureMessageKey =>
        FailureReason is { } reason ? LicenseFailureMapper.ToTranslationKey(reason) : null;

    /// <summary>
    /// The banner message for the current state. A detected clock rollback wins
    /// because it changes how every other date must be read.
    /// </summary>
    public string? BannerMessageKey
    {
        get
        {
            if (ClockTampered)
            {
                return TranslationKeys.License.ClockTamperBody;
            }

            return Status switch
            {
                LicenseStatus.ExpiringSoon => TranslationKeys.License.WarningExpiringSoon,
                LicenseStatus.GracePeriod => TranslationKeys.License.WarningGracePeriod,
                LicenseStatus.ReadOnly => TranslationKeys.License.WarningReadOnly,
                LicenseStatus.NotStarted => TranslationKeys.License.StartsOn,
                _ => null,
            };
        }
    }

    public string? BannerTitleKey => ClockTampered ? TranslationKeys.License.ClockTamperTitle : null;

    /// <summary>Parameters expected by <see cref="BannerMessageKey"/>.</summary>
    public IReadOnlyDictionary<string, string> BannerParameters
    {
        get
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);

            if (Status == LicenseStatus.ExpiringSoon)
            {
                values["days"] = DaysRemaining.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (Status == LicenseStatus.GracePeriod)
            {
                var daysPastExpiry = GraceDaysLeft is { } left ? LicenseStatusCalculator.GraceDays - left + 1 : 0;
                values["days"] = daysPastExpiry.ToString(System.Globalization.CultureInfo.InvariantCulture);
                values["graceLeft"] = (GraceDaysLeft ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture);
                values["date"] = FormatDate(ReadOnlyStartsOn);
            }
            else if (Status == LicenseStatus.NotStarted)
            {
                values["date"] = FormatDate(StartsOn);
            }

            return values;
        }
    }

    /// <summary>Honest grace message: days left and the day read-only begins.</summary>
    public string? GracePeriodDetailKey =>
        Status == LicenseStatus.GracePeriod ? TranslationKeys.License.GracePeriodDetail : null;

    private static string FormatDate(DateOnly? date) =>
        date is { } value ? LicenseDate.ToIso(value) : string.Empty;
}
