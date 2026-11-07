namespace SchoolManagement.Domain.Policies;

/// <summary>
/// One weekly slot reduced to the values the conflict rule needs. It is a pure
/// value so the rule can be tested without a database or a ViewModel.
/// </summary>
public readonly record struct ScheduleSlotCandidate(
    int SlotId,
    int GroupId,
    int? TeacherId,
    int DayOfWeek,
    TimeSpan Start,
    TimeSpan End);

public enum ScheduleConflictKind
{
    SameGroup = 0,
    SameTeacher = 1,
}

/// <summary>An existing slot that overlaps the candidate slot.</summary>
public sealed record ScheduleConflict(
    ScheduleConflictKind Kind,
    int SlotId,
    int GroupId,
    string? GroupName,
    int DayOfWeek,
    TimeSpan Start,
    TimeSpan End);

/// <summary>
/// Weekly timetable rules. Conflicts are warnings, never hard blocks: the
/// original phone app only warned about capacity and this desktop port keeps
/// the same spirit, because a school may deliberately schedule two groups in
/// the same room with one teacher during a transition week and the
/// administrator must stay in control. The caller asks for confirmation and
/// saves the slot only when the user accepts.
/// </summary>
public static class ScheduleConflictPolicy
{
    /// <summary>Half-open interval: 08:00-09:00 and 09:00-10:00 do not overlap.</summary>
    public static bool Overlaps(TimeSpan firstStart, TimeSpan firstEnd, TimeSpan secondStart, TimeSpan secondEnd)
        => firstStart < secondEnd && secondStart < firstEnd;

    public static bool IsValidRange(TimeSpan start, TimeSpan end) => end > start;

    public static bool IsValidDay(int dayOfWeek) => dayOfWeek is >= 0 and <= 6;

    /// <summary>
    /// Returns every existing slot of the same day that overlaps. A slot is
    /// excluded when it has the same id as the candidate (editing a slot never
    /// conflicts with itself).
    /// </summary>
    public static IReadOnlyList<ScheduleConflict> Detect(
        ScheduleSlotCandidate candidate,
        IEnumerable<ScheduleConflictSource> existing)
    {
        ArgumentNullException.ThrowIfNull(existing);

        var conflicts = new List<ScheduleConflict>();
        foreach (var slot in existing)
        {
            if (slot.SlotId == candidate.SlotId && candidate.SlotId != 0)
            {
                continue;
            }

            if (slot.DayOfWeek != candidate.DayOfWeek)
            {
                continue;
            }

            if (!Overlaps(candidate.Start, candidate.End, slot.Start, slot.End))
            {
                continue;
            }

            if (slot.GroupId == candidate.GroupId)
            {
                conflicts.Add(new ScheduleConflict(
                    ScheduleConflictKind.SameGroup,
                    slot.SlotId,
                    slot.GroupId,
                    slot.GroupName,
                    slot.DayOfWeek,
                    slot.Start,
                    slot.End));
                continue;
            }

            if (candidate.TeacherId is { } teacherId
                && slot.TeacherId is { } otherTeacher
                && teacherId == otherTeacher)
            {
                conflicts.Add(new ScheduleConflict(
                    ScheduleConflictKind.SameTeacher,
                    slot.SlotId,
                    slot.GroupId,
                    slot.GroupName,
                    slot.DayOfWeek,
                    slot.Start,
                    slot.End));
            }
        }

        return conflicts;
    }
}

/// <summary>The conflict-relevant columns of an existing weekly slot.</summary>
public readonly record struct ScheduleConflictSource(
    int SlotId,
    int GroupId,
    string? GroupName,
    int? TeacherId,
    int DayOfWeek,
    TimeSpan Start,
    TimeSpan End);

/// <summary>
/// Combines a calendar date with a stored time-of-day for session generation.
/// The result is always a UTC <see cref="DateTime"/> and never reads a clock.
/// </summary>
public static class SessionSchedulePolicy
{
    public static DateTime Combine(DateTime date, DateTime timeOfDay)
        => new DateTime(
            date.Year,
            date.Month,
            date.Day,
            timeOfDay.Hour,
            timeOfDay.Minute,
            timeOfDay.Second,
            DateTimeKind.Utc);

    public static DateTime Combine(DateTime date, TimeSpan timeOfDay)
        => new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc).Add(timeOfDay);

    public static bool IsValidRange(DateTime start, DateTime end) => end > start;
}
