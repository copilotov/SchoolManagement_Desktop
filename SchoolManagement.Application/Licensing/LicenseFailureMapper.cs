using SchoolManagement.Application.Localization;
using SchoolManagement.Domain.Licensing;

namespace SchoolManagement.Application.Licensing;

/// <summary>
/// Single place that maps a structured failure reason to a translation key, so
/// every layer (repository, guard, ViewModel) shows the same message.
/// </summary>
public static class LicenseFailureMapper
{
    public static string ToTranslationKey(LicenseFailureReason reason) => reason switch
    {
        LicenseFailureReason.Malformed => TranslationKeys.License.ErrorMalformed,
        LicenseFailureReason.BadSignature => TranslationKeys.License.ErrorBadSignature,
        LicenseFailureReason.FingerprintMismatch => TranslationKeys.License.ErrorFingerprintMismatch,
        LicenseFailureReason.UnsupportedVersion => TranslationKeys.License.ErrorUnsupportedVersion,
        LicenseFailureReason.NotConfigured => TranslationKeys.License.ErrorNotConfigured,
        _ => TranslationKeys.License.ErrorMalformed,
    };
}
