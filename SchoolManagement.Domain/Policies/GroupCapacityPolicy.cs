namespace SchoolManagement.Domain.Policies;

/// <summary>
/// Result of checking a group capacity. The original application warns when a
/// group is full but never blocks the enrollment; this type keeps that rule.
/// </summary>
public readonly record struct GroupCapacity(int MaxStudents, int ActiveEnrollments)
{
    public int RemainingSeats => Math.Max(0, MaxStudents - ActiveEnrollments);

    /// <summary>Full at <c>active &gt;= max</c>, exactly like the phone app.</summary>
    public bool IsFull => ActiveEnrollments >= MaxStudents;

    public bool IsOverCapacity => ActiveEnrollments > MaxStudents;

    /// <summary>Capacity is a warning, never a hard block.</summary>
    public bool ShouldWarn => IsFull;

    /// <summary>Enrollment is always allowed by policy; the caller shows a warning.</summary>
    public bool CanEnroll => true;
}

public static class GroupCapacityPolicy
{
    /// <summary>Default used by the original group table.</summary>
    public const int DefaultMaxStudents = 30;

    public static GroupCapacity Evaluate(int maxStudents, int activeEnrollments)
    {
        if (maxStudents < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxStudents), maxStudents, "Max students cannot be negative.");
        }

        if (activeEnrollments < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(activeEnrollments), activeEnrollments, "Active enrollments cannot be negative.");
        }

        return new GroupCapacity(maxStudents, activeEnrollments);
    }

    /// <summary>
    /// An unknown group produces no warning. This overload makes that explicit
    /// instead of letting a caller accidentally treat a missing group as full.
    /// </summary>
    public static GroupCapacity? EvaluateForKnownGroup(int? maxStudents, int activeEnrollments)
        => maxStudents is null ? null : Evaluate(maxStudents.Value, activeEnrollments);
}
