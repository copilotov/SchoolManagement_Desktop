namespace SchoolManagement.Domain.Common;

/// <summary>
/// The school week used by the original application.
/// 0 = Saturday ... 6 = Friday. This is intentionally different from
/// <see cref="DayOfWeek"/> (Sunday = 0 in .NET).
/// </summary>
public enum WeekDay
{
    Saturday = 0,
    Sunday = 1,
    Monday = 2,
    Tuesday = 3,
    Wednesday = 4,
    Thursday = 5,
    Friday = 6,
}

/// <summary>
/// Pure conversion between a calendar date and the school week.
/// No I/O, no UI, no culture-dependent parsing.
/// </summary>
public static class WeekDayCalculator
{
    /// <summary>
    /// Correct formula: ((int)date.DayOfWeek + 1) % 7.
    /// The broken formula (weekday % 7) shifts every day by one and caused
    /// sessions to be generated on the previous day in the Flutter app.
    /// </summary>
    public static WeekDay FromDate(DateTime date) => (WeekDay)(((int)date.DayOfWeek + 1) % 7);

    public static WeekDay FromDate(DateOnly date) => (WeekDay)(((int)date.DayOfWeek + 1) % 7);

    public static int ToIndex(WeekDay day) => (int)day;

    public static WeekDay FromIndex(int index)
    {
        if (index is < 0 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "A school weekday must be between 0 and 6.");
        }

        return (WeekDay)index;
    }

    /// <summary>Returns a translation key, never a localized string.</summary>
    public static string ToTranslationKey(WeekDay day) => day switch
    {
        WeekDay.Saturday => "days.saturday",
        WeekDay.Sunday => "days.sunday",
        WeekDay.Monday => "days.monday",
        WeekDay.Tuesday => "days.tuesday",
        WeekDay.Wednesday => "days.wednesday",
        WeekDay.Thursday => "days.thursday",
        WeekDay.Friday => "days.friday",
        _ => throw new ArgumentOutOfRangeException(nameof(day), day, "Unknown school weekday."),
    };
}
