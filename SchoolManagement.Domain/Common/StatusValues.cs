namespace SchoolManagement.Domain.Common;

// The phone database stores these as TEXT. They are kept as constants instead
// of enums so unknown values written by a future phone build are preserved and
// can be read tolerantly. The UI maps them to translation keys.

public static class StudentStatuses
{
    public const string Active = "active";
    public const string Inactive = "inactive";
    public const string Withdrawn = "withdrawn";
}

public static class EnrollmentStatuses
{
    public const string Active = "active";
    public const string Inactive = "inactive";
    public const string Withdrawn = "withdrawn";
}

public static class AttendanceStatuses
{
    public const string Present = "present";
    public const string Absent = "absent";
    public const string Late = "late";
    public const string Unexcused = "unexcused";

    /// <summary>
    /// The phone only counted "absent". "unexcused" is read tolerantly as an
    /// absence because older and newer builds use both values.
    /// </summary>
    public static bool IsAbsence(string? status) =>
        string.Equals(status, Absent, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, Unexcused, StringComparison.OrdinalIgnoreCase);
}

public static class SessionTypes
{
    public const string Regular = "regular";
    public const string Makeup = "makeup";
    public const string Extra = "extra";
}

public static class SessionStatuses
{
    public const string Scheduled = "scheduled";
    public const string InProgress = "in_progress";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
    public const string Postponed = "postponed";
}

public static class PaymentTypes
{
    public const string Monthly = "monthly";
    public const string Registration = "registration";
    public const string Advance = "advance";
    public const string Extra = "extra";
}

public static class PaymentMethods
{
    public const string Cash = "cash";
    public const string BankTransfer = "bank_transfer";
    public const string Check = "check";
    public const string Other = "other";
}

public static class PaymentStatuses
{
    public const string Unpaid = "unpaid";
    public const string Partial = "partial";
    public const string Paid = "paid";
    public const string Exempted = "exempted";
}

public static class HomeworkStatuses
{
    public const string Pending = "pending";
    public const string Submitted = "submitted";
    public const string Late = "late";
    public const string Missing = "missing";
}

public static class ExemptionTypes
{
    public const string Full = "full";
    public const string Partial = "partial";
}

public static class ExtraFeeStatuses
{
    public const string Unpaid = "unpaid";
    public const string Partial = "partial";
    public const string Paid = "paid";
}

public static class NotificationTypes
{
    public const string Attendance = "attendance";
    public const string Payment = "payment";
    public const string Session = "session";
    public const string General = "general";
}

public static class NotificationChannels
{
    public const string InApp = "in_app";
    public const string Sms = "sms";
    public const string Email = "email";
}

public static class RecipientTypes
{
    public const string Student = "student";
    public const string Guardian = "guardian";
    public const string Teacher = "teacher";
    public const string All = "all";
}

public static class SmsStatuses
{
    public const string Sent = "sent";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}
