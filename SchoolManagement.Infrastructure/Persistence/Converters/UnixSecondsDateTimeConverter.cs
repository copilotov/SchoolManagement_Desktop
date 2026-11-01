using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SchoolManagement.Infrastructure.Persistence.Converters;

/// <summary>
/// Drift stores <see cref="DateTime"/> as Unix epoch seconds (INTEGER) and reads
/// them back as local wall-clock values. To keep every date coming from the
/// phone file identical, this converter writes the wall-clock components as
/// local time and reads the stored instant back as local components labelled
/// <see cref="DateTimeKind.Utc"/> (the convention used by the domain).
/// Round-tripping a phone value therefore always produces the same seconds.
/// </summary>
public sealed class UnixSecondsDateTimeConverter : ValueConverter<DateTime, long>
{
    public UnixSecondsDateTimeConverter()
        : base(value => ToUnixSeconds(value), seconds => FromUnixSeconds(seconds))
    {
    }

    public static long ToUnixSeconds(DateTime value)
    {
        // The domain uses Kind=Utc to mean "calendar value", so the components
        // are interpreted as local wall-clock time before becoming an instant.
        var wallClock = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        var offset = TimeZoneInfo.Local.GetUtcOffset(wallClock);
        return new DateTimeOffset(wallClock, offset).ToUnixTimeSeconds();
    }

    public static DateTime FromUnixSeconds(long seconds)
    {
        var local = DateTimeOffset.FromUnixTimeSeconds(seconds).ToLocalTime().DateTime;
        return DateTime.SpecifyKind(local, DateTimeKind.Utc);
    }
}

/// <summary>Nullable companion of <see cref="UnixSecondsDateTimeConverter"/>.</summary>
public sealed class NullableUnixSecondsDateTimeConverter : ValueConverter<DateTime?, long?>
{
    public NullableUnixSecondsDateTimeConverter()
        : base(
            value => value == null ? null : UnixSecondsDateTimeConverter.ToUnixSeconds(value.Value),
            seconds => seconds == null ? null : UnixSecondsDateTimeConverter.FromUnixSeconds(seconds.Value))
    {
    }
}
