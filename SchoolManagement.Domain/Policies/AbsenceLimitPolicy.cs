namespace SchoolManagement.Domain.Policies;

/// <summary>
/// Absence limit rule used by the attendance notification. The phone triggers
/// a notification as soon as <c>absentCount &gt;= limit</c> and suppresses
/// duplicates while an unread attendance notification exists.
/// </summary>
public readonly record struct AbsenceLimitStatus(int AbsenceCount, int Limit)
{
    public bool HasReachedLimit => Limit > 0 && AbsenceCount >= Limit;

    public bool IsExceeded => AbsenceCount > Limit;

    public int RemainingBeforeLimit => Math.Max(0, Limit - AbsenceCount);
}

public static class AbsenceLimitPolicy
{
    /// <summary>Default when <c>system_settings.absence_limit</c> is missing.</summary>
    public const int DefaultLimit = 5;

    public const string SettingKey = "absence_limit";

    public static AbsenceLimitStatus Evaluate(int absenceCount, int limit)
    {
        if (absenceCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(absenceCount), absenceCount, "Absence count cannot be negative.");
        }

        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "The absence limit must be positive.");
        }

        return new AbsenceLimitStatus(absenceCount, limit);
    }

    /// <summary>Reads a stored text value tolerantly and falls back to 5.</summary>
    public static int ParseLimit(string? storedValue)
    {
        if (int.TryParse(storedValue, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        return DefaultLimit;
    }
}
