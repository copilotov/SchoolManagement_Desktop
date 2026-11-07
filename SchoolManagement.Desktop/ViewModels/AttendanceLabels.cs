using SchoolManagement.Application.Localization;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>
/// Maps the TEXT status values of the phone schema to translation keys. The
/// mapping is kept in one place so the attendance list, the take-attendance
/// screen and the homework report never repeat it, and so an unknown value
/// written by a future phone build is displayed as-is instead of disappearing.
/// </summary>
internal static class AttendanceLabels
{
    public static string Status(ILocalizationService localization, string? status)
    {
        ArgumentNullException.ThrowIfNull(localization);

        return status?.Trim().ToLowerInvariant() switch
        {
            SessionStatuses.Scheduled => localization.Tr(TranslationKeys.Attendance.Scheduled),
            SessionStatuses.InProgress => localization.Tr(TranslationKeys.Attendance.InProgress),
            SessionStatuses.Completed => localization.Tr(TranslationKeys.Attendance.Completed),
            SessionStatuses.Cancelled => localization.Tr(TranslationKeys.Attendance.Cancelled),
            SessionStatuses.Postponed => localization.Tr(TranslationKeys.Attendance.Postponed),
            _ => status ?? string.Empty,
        };
    }

    public static string SessionType(ILocalizationService localization, string? type)
    {
        ArgumentNullException.ThrowIfNull(localization);

        return type?.Trim().ToLowerInvariant() switch
        {
            SessionTypes.Regular => localization.Tr(TranslationKeys.Attendance.Regular),
            SessionTypes.Makeup => localization.Tr(TranslationKeys.Attendance.Makeup),
            SessionTypes.Extra => localization.Tr(TranslationKeys.Attendance.Extra),
            _ => type ?? string.Empty,
        };
    }

    public static string PaymentStatus(ILocalizationService localization, string? status)
    {
        ArgumentNullException.ThrowIfNull(localization);

        return status?.Trim().ToLowerInvariant() switch
        {
            PaymentStatuses.Paid => localization.Tr(TranslationKeys.Attendance.Paid),
            PaymentStatuses.Partial => localization.Tr(TranslationKeys.Attendance.Partial),
            PaymentStatuses.Exempted => localization.Tr(TranslationKeys.Attendance.Exempt),
            PaymentStatuses.Unpaid => localization.Tr(TranslationKeys.Attendance.Unpaid),
            _ => string.Empty,
        };
    }

    public static string AttendanceStatus(ILocalizationService localization, string? status)
    {
        ArgumentNullException.ThrowIfNull(localization);

        return status?.Trim().ToLowerInvariant() switch
        {
            AttendanceStatuses.Present => localization.Tr(TranslationKeys.Attendance.Present),
            AttendanceStatuses.Absent => localization.Tr(TranslationKeys.Attendance.Absent),
            AttendanceStatuses.Late => localization.Tr(TranslationKeys.Attendance.Late),
            AttendanceStatuses.Unexcused => localization.Tr(TranslationKeys.Attendance.Absent),
            _ => status ?? string.Empty,
        };
    }

    public static string HomeworkStatus(ILocalizationService localization, string? status)
    {
        ArgumentNullException.ThrowIfNull(localization);

        return status?.Trim().ToLowerInvariant() switch
        {
            HomeworkStatuses.Submitted => localization.Tr(TranslationKeys.Homework.Submitted),
            HomeworkStatuses.Late => localization.Tr(TranslationKeys.Homework.Late),
            HomeworkStatuses.Missing => localization.Tr(TranslationKeys.Homework.Missing),
            HomeworkStatuses.Pending => localization.Tr(TranslationKeys.Homework.Pending),
            _ => status ?? string.Empty,
        };
    }

    /// <summary>Localized school-week day ("Saturday" ... "Friday") of a date.</summary>
    public static string DayName(ILocalizationService localization, DateTime date)
    {
        ArgumentNullException.ThrowIfNull(localization);

        return localization.Tr(
            WeekDayCalculator.ToTranslationKey(WeekDayCalculator.FromDate(date)));
    }
}
