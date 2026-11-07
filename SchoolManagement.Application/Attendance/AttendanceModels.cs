using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Policies;

namespace SchoolManagement.Application.Attendance;

/// <summary>
/// Deliberate session query. The date range, group, status and type filters all
/// run in SQL; the projection only carries the columns the attendance table
/// displays.
/// </summary>
public sealed record SessionListQuery(
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int? GroupId = null,
    string? Status = null,
    string? Type = null);

/// <summary>A status change requested from the attendance list.</summary>
public sealed record SessionStatusChangeRequest(
    int SessionId,
    string Status,
    string? CancelReason = null);

public sealed record SessionStatusChangeResult(string Status, bool SuggestsSms);

/// <summary>One row submitted by the "take attendance" screen.</summary>
public sealed record AttendanceEntry(int StudentId, string Status, string? Notes = null);

/// <summary>
/// Bulk save of one session. The session is marked completed after the records
/// are written, then the absence limit notification is evaluated.
/// </summary>
public sealed record SaveSessionAttendanceRequest(
    int SessionId,
    IReadOnlyList<AttendanceEntry> Entries,
    bool MarkCompleted = true);

public sealed record SessionAttendanceOutcome(
    int SessionId,
    int SavedCount,
    int AbsentCount,
    int NotificationsCreated);

/// <summary>Everything the take-attendance screen needs in one read.</summary>
public sealed record SessionAttendanceBoard(
    SessionListItem Session,
    IReadOnlyList<AttendanceRosterItem> Roster,
    IReadOnlyList<HomeworkListItem> DueHomeworks);

/// <summary>An extra or makeup session created from the attendance screen.</summary>
public sealed record ExtraSessionRequest(
    int GroupId,
    DateTime SessionDate,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string Type = SessionTypes.Extra,
    string? LessonTopic = null,
    string? TeacherNotes = null,
    double? Price = null);

public sealed record ExtraSessionOutcome(int SessionId, bool Created);

/// <summary>"paid" records a payment row, "exempted" writes the exemption status.</summary>
public sealed record SessionPaymentRequest(
    int SessionId,
    string Mode,
    double Amount = 0,
    string Method = PaymentMethods.Cash,
    string? Notes = null);

public sealed record SessionPaymentOutcome(int SessionId, int? PaymentId, string PaymentStatus);

public sealed record SessionPaymentModes
{
    public const string Paid = "paid";
    public const string Exempted = "exempted";
}

/// <summary>Student and group period report.</summary>
public sealed record AttendanceReportQuery(
    DateTime FromDate,
    DateTime ToDate,
    int? StudentId = null,
    int? GroupId = null);

/// <summary>The immutable bundle handed to the PDF/Excel ports.</summary>
public sealed record AttendanceReportData(
    string SchoolName,
    DateTime FromDate,
    DateTime ToDate,
    int? GroupId,
    string? GroupName,
    int? StudentId,
    string? StudentName,
    IReadOnlyList<AttendanceReportRow> Rows,
    DateTime GeneratedAt)
{
    public int TotalRecords => Rows.Sum(row => row.SessionCount);

    public int TotalPresent => Rows.Sum(row => row.PresentCount);

    public int TotalAbsent => Rows.Sum(row => row.AbsentCount);

    public int TotalLate => Rows.Sum(row => row.LateCount);

    public int StudentCount => Rows.Select(row => row.StudentId).Distinct().Count();

    public double AttendanceRate => TotalRecords == 0
        ? 0
        : (TotalPresent + TotalLate) * 100d / TotalRecords;
}

/// <summary>An SMS suggestion after a cancellation/postponement.</summary>
public sealed record SessionSmsRequest(int SessionId, string EventCode);

public sealed record SessionSmsOutcome(
    int LogCount,
    IReadOnlyList<string> Phones,
    string Body);

/// <summary>Pure validation shared by the attendance and extra-session screens.</summary>
public static class AttendanceValidator
{
    public const int MaxPrice = 1_000_000;

    public static Failure? ValidateStatusChange(SessionStatusChangeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!SessionStatusPolicy.IsValidStatus(request.Status))
        {
            return Failure.Validation(TranslationKeys.Validators.InvalidField, "status");
        }

        return null;
    }

    public static Failure? ValidateBulkSave(SaveSessionAttendanceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.SessionId <= 0)
        {
            return Failure.Validation(TranslationKeys.Errors.RequiredFields, "sessionId");
        }

        if (request.Entries.Count == 0)
        {
            return Failure.Validation(TranslationKeys.Errors.RequiredFields, "entries");
        }

        foreach (var entry in request.Entries)
        {
            if (entry.StudentId <= 0)
            {
                return Failure.Validation(TranslationKeys.Errors.RequiredFields, "studentId");
            }

            if (!IsValidAttendanceStatus(entry.Status))
            {
                return Failure.Validation(TranslationKeys.Validators.InvalidField, "status");
            }
        }

        return null;
    }

    public static Failure? ValidateExtraSession(ExtraSessionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.GroupId <= 0)
        {
            return Failure.Validation(TranslationKeys.Validators.Required, "groupId");
        }

        if (request.SessionDate == default)
        {
            return Failure.Validation(TranslationKeys.Validators.Required, "sessionDate");
        }

        if (!SessionSchedulePolicy.IsValidRange(
                request.SessionDate.Add(request.StartTime),
                request.SessionDate.Add(request.EndTime)))
        {
            return Failure.Validation(TranslationKeys.Attendance.InvalidTimeRange, "endTime");
        }

        if (!string.Equals(request.Type, SessionTypes.Makeup, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(request.Type, SessionTypes.Extra, StringComparison.OrdinalIgnoreCase))
        {
            return Failure.Validation(TranslationKeys.Validators.InvalidField, "type");
        }

        if (request.Price is < 0 or > MaxPrice)
        {
            return Failure.Validation(TranslationKeys.Validators.PositiveNumber, "price");
        }

        return null;
    }

    public static Failure? ValidatePayment(SessionPaymentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.SessionId <= 0)
        {
            return Failure.Validation(TranslationKeys.Errors.RequiredFields, "sessionId");
        }

        if (!string.Equals(request.Mode, SessionPaymentModes.Paid, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(request.Mode, SessionPaymentModes.Exempted, StringComparison.OrdinalIgnoreCase))
        {
            return Failure.Validation(TranslationKeys.Validators.InvalidField, "mode");
        }

        if (request.Amount < 0 || request.Amount > MaxPrice)
        {
            return Failure.Validation(TranslationKeys.Validators.PositiveNumber, "amount");
        }

        return null;
    }

    public static bool IsValidAttendanceStatus(string? status)
        => string.Equals(status, AttendanceStatuses.Present, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, AttendanceStatuses.Absent, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, AttendanceStatuses.Late, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, AttendanceStatuses.Unexcused, StringComparison.OrdinalIgnoreCase);

    public static bool IsAbsence(string? status) => AttendanceStatuses.IsAbsence(status);
}
