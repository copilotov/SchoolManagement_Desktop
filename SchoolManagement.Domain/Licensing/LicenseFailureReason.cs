namespace SchoolManagement.Domain.Licensing;

public enum LicenseFailureReason
{
    Malformed,
    BadSignature,
    FingerprintMismatch,
    UnsupportedVersion,
    NotConfigured
}
