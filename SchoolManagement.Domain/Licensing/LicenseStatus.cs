namespace SchoolManagement.Domain.Licensing;

public enum LicenseStatus
{
    Active,
    ExpiringSoon,
    GracePeriod,
    ReadOnly,
    NotStarted,
    Unlicensed,
    Invalid
}
