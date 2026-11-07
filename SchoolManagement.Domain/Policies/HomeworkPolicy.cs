using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Policies;

/// <summary>
/// Pure homework/submission rules. A submission is "late" when it arrives after
/// the due date, "missing" when the due date passed without a submission, and
/// "pending" otherwise. The comparison uses the last instant of the due day so
/// a submission on the due date is never marked late.
/// </summary>
public static class HomeworkPolicy
{
    public static DateTime EndOfDay(DateTime date)
        => new(date.Year, date.Month, date.Day, 23, 59, 59, DateTimeKind.Utc);

    public static bool IsOverdue(DateTime dueDate, DateTime today)
        => today.Date > dueDate.Date;

    /// <summary>Due homeworks of a session are shown in the attendance screen.</summary>
    public static bool IsDueForSession(int? targetSessionId, int sessionId, bool isCompleted)
        => targetSessionId == sessionId && !isCompleted;

    public static bool CountsAsSubmitted(string? status)
        => string.Equals(status, HomeworkStatuses.Submitted, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, HomeworkStatuses.Late, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Resolves the status that should be stored for one student. A submitted
    /// timestamp always wins; the due date only distinguishes submitted/late.
    /// </summary>
    public static string Resolve(DateTime? submittedAt, DateTime dueDate, DateTime today)
    {
        if (submittedAt is { } submitted)
        {
            return submitted.Date > dueDate.Date
                ? HomeworkStatuses.Late
                : HomeworkStatuses.Submitted;
        }

        return IsOverdue(dueDate, today)
            ? HomeworkStatuses.Missing
            : HomeworkStatuses.Pending;
    }

    /// <summary>Resolves the display status of an existing row.</summary>
    public static string ResolveExisting(
        string? storedStatus,
        DateTime? submittedAt,
        DateTime dueDate,
        DateTime today)
    {
        if (CountsAsSubmitted(storedStatus))
        {
            return string.Equals(storedStatus, HomeworkStatuses.Late, StringComparison.OrdinalIgnoreCase)
                ? HomeworkStatuses.Late
                : HomeworkStatuses.Submitted;
        }

        return Resolve(submittedAt, dueDate, today);
    }
}
