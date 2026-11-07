using System.Globalization;

namespace SchoolManagement.Domain.Licensing;

/// <summary>
/// Business dates in a license are calendar days written as <c>yyyy-MM-dd</c>.
/// They are parsed by hand and materialised as UTC midnight, never with
/// <see cref="DateTime.Parse(string)"/> or the device's local time zone, so a
/// license behaves the same on every machine.
/// </summary>
public static class LicenseDate
{
    public const string Format = "yyyy-MM-dd";

    public static string ToIso(DateOnly date) =>
        string.Concat(
            date.Year.ToString("D4", CultureInfo.InvariantCulture),
            "-",
            date.Month.ToString("D2", CultureInfo.InvariantCulture),
            "-",
            date.Day.ToString("D2", CultureInfo.InvariantCulture));

    public static bool TryParse(ReadOnlySpan<char> value, out DateOnly date)
    {
        date = default;
        if (value.Length != 10 || value[4] != '-' || value[7] != '-')
        {
            return false;
        }

        if (!TryParseDigits(value[..4], out var year) ||
            !TryParseDigits(value.Slice(5, 2), out var month) ||
            !TryParseDigits(value.Slice(8, 2), out var day))
        {
            return false;
        }

        if (year < 1 || month < 1 || month > 12 || day < 1 || day > 31)
        {
            return false;
        }

        try
        {
            // Explicit UTC constructor, never DateTime.Parse/DateOnly.Parse.
            var utc = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
            date = DateOnly.FromDateTime(utc);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    public static DateTime ToUtcMidnight(DateOnly date) =>
        new(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc);

    private static bool TryParseDigits(ReadOnlySpan<char> value, out int result)
    {
        result = 0;
        if (value.IsEmpty)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }

            result = (result * 10) + (c - '0');
        }

        return true;
    }
}
