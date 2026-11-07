using SchoolManagement.Domain.Licensing;

namespace SchoolManagement.Application.Licensing;

public enum TrustedTimeState
{
    Consistent,
    Rollback,
    Advanced,
}

public sealed record TrustedTimeVerdict(
    DateTime TrustedUtc,
    DateTime DeviceUtc,
    DateTime DeviceLocal,
    TrustedTimeState State,
    bool ClockTampered,
    TrustedTimeRecord Updated);

/// <summary>
/// Pure three-way merge of (device clock, last trusted instant, license issue
/// anchor). Rules copied from the phone:
/// <list type="bullet">
/// <item>the trusted value is the highest of the three and is never lowered;</item>
/// <item>a device more than five minutes behind the stored instant is a rollback;</item>
/// <item>the <c>iat</c> anchor applies even when nothing is stored yet, so a
/// clock rolled back before activation is still detected;</item>
/// <item>an anchor before the year 2000 is corrupt and ignored.</item>
/// </list>
/// The anchor is compared by local calendar day with a one-day grace, because a
/// time-zone change must not look like tampering.
/// </summary>
public static class TrustedTimeEngine
{
    public static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(5);

    /// <summary>Time zones move the local date by at most one day from UTC.</summary>
    public const int AnchorGraceDays = 1;

    public static TrustedTimeVerdict Evaluate(
        DateTime deviceUtc,
        DateTime deviceLocal,
        TrustedTimeRecord? stored,
        DateOnly? anchorDate)
    {
        var device = AsUtc(deviceUtc);
        var storedLast = NormalizeStored(stored);
        var anchor = NormalizeAnchor(anchorDate);

        var rollback = DetectRollback(device, deviceLocal, storedLast, anchorDate);

        var floor = Max(storedLast, anchor);
        var trusted = rollback
            ? floor ?? device
            : Max(device, floor);

        var wasTampered = stored?.NoticedRollback == true;
        var clockTampered = rollback || (wasTampered && !HasRecovered(device, deviceLocal, storedLast, anchorDate));

        var state = rollback
            ? TrustedTimeState.Rollback
            : storedLast is { } last && device > last
                ? TrustedTimeState.Advanced
                : TrustedTimeState.Consistent;

        var highest = Max(Max(trusted, storedLast), anchor);
        var updated = new TrustedTimeRecord(highest, device, clockTampered);

        return new TrustedTimeVerdict(trusted, device, AsLocal(deviceLocal), state, clockTampered, updated);
    }

    private static bool DetectRollback(
        DateTime device,
        DateTime deviceLocal,
        DateTime? storedLast,
        DateOnly? anchorDate)
    {
        if (storedLast is { } last && device < last - Tolerance)
        {
            return true;
        }

        if (anchorDate is { } anchor && anchor.Year >= 2000)
        {
            var deviceDate = DateOnly.FromDateTime(AsLocal(deviceLocal));
            if (deviceDate < anchor.AddDays(-AnchorGraceDays))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasRecovered(
        DateTime device,
        DateTime deviceLocal,
        DateTime? storedLast,
        DateOnly? anchorDate)
    {
        if (storedLast is { } last && device < last - Tolerance)
        {
            return false;
        }

        if (anchorDate is { Year: >= 2000 } anchor)
        {
            var deviceDate = DateOnly.FromDateTime(AsLocal(deviceLocal));
            if (deviceDate < anchor.AddDays(-AnchorGraceDays))
            {
                return false;
            }
        }

        return true;
    }

    private static DateTime? NormalizeStored(TrustedTimeRecord? stored)
    {
        if (stored is null)
        {
            return null;
        }

        var value = AsUtc(stored.LastKnownUtc);
        return value.Year < 2000 ? null : value;
    }

    private static DateTime? NormalizeAnchor(DateOnly? anchorDate)
    {
        if (anchorDate is not { Year: >= 2000 } anchor)
        {
            return null;
        }

        return LicenseDate.ToUtcMidnight(anchor);
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    private static DateTime AsLocal(DateTime value) => value.Kind switch
    {
        DateTimeKind.Local => DateTime.SpecifyKind(value, DateTimeKind.Unspecified),
        _ => value,
    };

    private static DateTime? Max(DateTime? first, DateTime? second)
    {
        if (first is null)
        {
            return second;
        }

        if (second is null)
        {
            return first;
        }

        return first.Value >= second.Value ? first : second;
    }

    private static DateTime Max(DateTime first, DateTime? second) =>
        second is { } value && value > first ? value : first;
}
