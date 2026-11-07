using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Policies;

/// <summary>Outcome of a requested session status transition.</summary>
public readonly record struct SessionStatusTransition(bool Allowed, bool SuggestsSms);

/// <summary>
/// Pure session status state machine. The phone app allowed a session to be
/// cancelled or postponed from the attendance list; this class keeps that rule
/// out of the ViewModel so it is tested without a database or a WPF loop.
///
/// The stored values are TEXT and are read tolerantly: an unknown status is not
/// blocked forever, it is treated as "restorable" so a future phone build can
/// never lock a session out of the desktop app.
/// </summary>
public static class SessionStatusPolicy
{
    private static readonly string[] AllStatuses =
    [
        SessionStatuses.Scheduled,
        SessionStatuses.InProgress,
        SessionStatuses.Completed,
        SessionStatuses.Cancelled,
        SessionStatuses.Postponed,
    ];

    public static IReadOnlyList<string> AllowedStatuses => AllStatuses;

    public static bool IsValidStatus(string? status)
        => status is not null && AllStatuses.Contains(status, StringComparer.OrdinalIgnoreCase);

    /// <summary>Completed and cancelled are final; postponing again is allowed.</summary>
    public static bool IsTerminal(string? status)
        => string.Equals(status, SessionStatuses.Completed, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, SessionStatuses.Cancelled, StringComparison.OrdinalIgnoreCase);

    /// <summary>Attendance may only be taken for a session that is still open.</summary>
    public static bool CanTakeAttendance(string? status)
        => string.Equals(status, SessionStatuses.Scheduled, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, SessionStatuses.InProgress, StringComparison.OrdinalIgnoreCase);

    /// <summary>Cancelling or postponing opens the optional SMS suggestion.</summary>
    public static bool SuggestsSms(string? status)
        => string.Equals(status, SessionStatuses.Cancelled, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, SessionStatuses.Postponed, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A transition to the same status is a no-op and stays allowed so an
    /// accidental double click never shows an error.
    /// </summary>
    public static bool CanTransition(string? from, string? to)
    {
        if (!IsValidStatus(to))
        {
            return false;
        }

        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Unknown stored value: allow any valid target instead of locking the
        // row. This is the tolerant-reading rule of the phone schema.
        if (!IsValidStatus(from) || string.IsNullOrWhiteSpace(from))
        {
            return true;
        }

        return from!.ToLowerInvariant() switch
        {
            SessionStatuses.Scheduled => true,
            SessionStatuses.InProgress => true,
            SessionStatuses.Postponed => true,
            SessionStatuses.Completed => false,
            SessionStatuses.Cancelled => false,
            _ => true,
        };
    }

    public static SessionStatusTransition Evaluate(string? from, string? to)
        => new(CanTransition(from, to), SuggestsSms(to));
}
