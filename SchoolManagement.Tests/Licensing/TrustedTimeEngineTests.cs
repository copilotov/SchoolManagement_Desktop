using FluentAssertions;
using SchoolManagement.Application.Licensing;

namespace SchoolManagement.Tests.Licensing;

public sealed class TrustedTimeEngineTests
{
    private static readonly DateTime Device = LicenseTestKit.Utc(2026, 6, 1, 10);

    private static TrustedTimeVerdict Evaluate(
        DateTime? deviceUtc = null,
        DateTime? deviceLocal = null,
        TrustedTimeRecord? stored = null,
        DateOnly? anchor = null) =>
        TrustedTimeEngine.Evaluate(
            deviceUtc ?? Device,
            deviceLocal ?? DateTime.SpecifyKind(deviceUtc ?? Device, DateTimeKind.Unspecified),
            stored,
            anchor);

    private static TrustedTimeRecord Stored(DateTime lastKnown, bool tampered = false) =>
        new(lastKnown, lastKnown, tampered);

    [Fact]
    public void With_nothing_stored_the_device_clock_is_trusted()
    {
        var verdict = Evaluate();

        verdict.TrustedUtc.Should().Be(Device);
        verdict.State.Should().Be(TrustedTimeState.Consistent);
        verdict.ClockTampered.Should().BeFalse();
        verdict.Updated.LastKnownUtc.Should().Be(Device);
        verdict.Updated.ObservedAtUtc.Should().Be(Device);
    }

    [Fact]
    public void An_anchor_in_the_past_does_not_move_the_clock_forward()
    {
        var verdict = Evaluate(anchor: new DateOnly(2026, 1, 1));

        verdict.TrustedUtc.Should().Be(Device);
        verdict.ClockTampered.Should().BeFalse();
    }

    [Fact]
    public void Rolling_the_clock_back_before_activation_is_detected_by_the_issue_anchor()
    {
        var verdict = Evaluate(
            deviceUtc: LicenseTestKit.Utc(2026, 5, 1, 10),
            anchor: new DateOnly(2026, 6, 1));

        verdict.State.Should().Be(TrustedTimeState.Rollback);
        verdict.ClockTampered.Should().BeTrue();
        verdict.TrustedUtc.Should().Be(LicenseTestKit.Utc(2026, 6, 1, 0));
    }

    [Fact]
    public void The_anchor_grace_day_is_not_tampering()
    {
        var verdict = Evaluate(
            deviceUtc: LicenseTestKit.Utc(2026, 5, 31, 10),
            anchor: new DateOnly(2026, 6, 1));

        verdict.ClockTampered.Should().BeFalse();
        verdict.TrustedUtc.Should().Be(LicenseTestKit.Utc(2026, 6, 1, 0));
    }

    [Fact]
    public void Two_days_behind_the_anchor_is_tampering()
    {
        var verdict = Evaluate(
            deviceUtc: LicenseTestKit.Utc(2026, 5, 30, 10),
            anchor: new DateOnly(2026, 6, 1));

        verdict.ClockTampered.Should().BeTrue();
        verdict.State.Should().Be(TrustedTimeState.Rollback);
    }

    [Fact]
    public void An_anchor_before_the_year_2000_is_ignored_as_corrupt()
    {
        var verdict = Evaluate(
            deviceUtc: LicenseTestKit.Utc(1990, 1, 1),
            anchor: new DateOnly(1995, 1, 1));

        verdict.ClockTampered.Should().BeFalse();
        verdict.TrustedUtc.Should().Be(LicenseTestKit.Utc(1990, 1, 1));
    }

    [Fact]
    public void A_stored_instant_before_2000_is_ignored()
    {
        var verdict = Evaluate(stored: Stored(LicenseTestKit.Utc(1999, 12, 31)));

        verdict.State.Should().Be(TrustedTimeState.Consistent);
        verdict.ClockTampered.Should().BeFalse();
    }

    [Fact]
    public void A_clock_more_than_five_minutes_behind_the_stored_instant_is_a_rollback()
    {
        var stored = Stored(Device.AddMinutes(6));

        var verdict = Evaluate(stored: stored);

        verdict.State.Should().Be(TrustedTimeState.Rollback);
        verdict.ClockTampered.Should().BeTrue();
        verdict.TrustedUtc.Should().Be(stored.LastKnownUtc);
    }

    [Fact]
    public void Exactly_five_minutes_behind_is_within_tolerance()
    {
        var verdict = Evaluate(stored: Stored(Device.AddMinutes(5)));

        verdict.State.Should().Be(TrustedTimeState.Consistent);
        verdict.ClockTampered.Should().BeFalse();
        verdict.TrustedUtc.Should().Be(Device.AddMinutes(5));
    }

    [Fact]
    public void One_second_beyond_the_tolerance_is_a_rollback()
    {
        var verdict = Evaluate(stored: Stored(Device.AddMinutes(5).AddSeconds(1)));

        verdict.State.Should().Be(TrustedTimeState.Rollback);
    }

    [Fact]
    public void A_later_device_clock_advances_the_trusted_instant()
    {
        var verdict = Evaluate(stored: Stored(Device.AddDays(-1)));

        verdict.State.Should().Be(TrustedTimeState.Advanced);
        verdict.TrustedUtc.Should().Be(Device);
        verdict.Updated.LastKnownUtc.Should().Be(Device);
    }

    [Fact]
    public void The_trusted_instant_is_never_lowered()
    {
        var stored = Stored(Device.AddDays(10));

        var verdict = Evaluate(stored: stored);

        verdict.TrustedUtc.Should().Be(stored.LastKnownUtc);
        verdict.Updated.LastKnownUtc.Should().Be(stored.LastKnownUtc);
    }

    [Fact]
    public void The_device_observation_is_recorded_even_during_a_rollback()
    {
        var verdict = Evaluate(stored: Stored(Device.AddDays(3)));

        verdict.DeviceUtc.Should().Be(Device);
        verdict.Updated.ObservedAtUtc.Should().Be(Device);
    }

    [Fact]
    public void A_persisted_tamper_flag_survives_while_the_clock_is_still_wrong()
    {
        var verdict = Evaluate(stored: Stored(Device.AddDays(3), tampered: true));

        verdict.ClockTampered.Should().BeTrue();
        verdict.Updated.NoticedRollback.Should().BeTrue();
    }

    [Fact]
    public void A_persisted_tamper_flag_clears_once_the_clock_is_corrected()
    {
        var verdict = Evaluate(stored: Stored(Device.AddDays(-1), tampered: true));

        verdict.ClockTampered.Should().BeFalse();
        verdict.Updated.NoticedRollback.Should().BeFalse();
    }

    [Fact]
    public void A_persisted_tamper_flag_stays_while_the_anchor_is_still_in_the_future()
    {
        var verdict = Evaluate(
            deviceUtc: LicenseTestKit.Utc(2026, 5, 1),
            stored: Stored(LicenseTestKit.Utc(2026, 4, 1), tampered: true),
            anchor: new DateOnly(2026, 6, 1));

        verdict.ClockTampered.Should().BeTrue();
    }

    [Fact]
    public void A_time_zone_shift_forward_of_utc_is_not_tampering()
    {
        // UTC+14: local date is one day ahead of the UTC date.
        var verdict = Evaluate(
            deviceUtc: LicenseTestKit.Utc(2026, 3, 10, 23),
            deviceLocal: new DateTime(2026, 3, 11, 13, 0, 0, DateTimeKind.Unspecified),
            anchor: new DateOnly(2026, 3, 11));

        verdict.ClockTampered.Should().BeFalse();
        verdict.State.Should().Be(TrustedTimeState.Consistent);
    }

    [Fact]
    public void A_time_zone_shift_behind_utc_is_not_tampering()
    {
        // UTC-11: local date is one day behind the UTC date.
        var verdict = Evaluate(
            deviceUtc: LicenseTestKit.Utc(2026, 3, 10, 1),
            deviceLocal: new DateTime(2026, 3, 9, 15, 0, 0, DateTimeKind.Unspecified),
            anchor: new DateOnly(2026, 3, 10));

        verdict.ClockTampered.Should().BeFalse();
        verdict.State.Should().Be(TrustedTimeState.Consistent);
    }

    [Fact]
    public void A_two_day_shift_cannot_be_explained_by_a_time_zone()
    {
        var verdict = Evaluate(
            deviceUtc: LicenseTestKit.Utc(2026, 3, 8, 12),
            deviceLocal: new DateTime(2026, 3, 8, 12, 0, 0, DateTimeKind.Unspecified),
            anchor: new DateOnly(2026, 3, 10));

        verdict.ClockTampered.Should().BeTrue();
        verdict.TrustedUtc.Should().Be(LicenseTestKit.Utc(2026, 3, 10, 0));
    }

    [Fact]
    public void The_local_observation_is_returned_without_a_kind()
    {
        var local = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Unspecified);

        var verdict = Evaluate(deviceLocal: local);

        verdict.DeviceLocal.Should().Be(local);
        verdict.DeviceLocal.Kind.Should().Be(DateTimeKind.Unspecified);
    }

    [Fact]
    public void The_stored_instant_and_the_anchor_combine_to_the_highest_value()
    {
        var verdict = Evaluate(
            stored: Stored(LicenseTestKit.Utc(2026, 5, 1)),
            anchor: new DateOnly(2026, 7, 1));

        verdict.Updated.LastKnownUtc.Should().Be(LicenseTestKit.Utc(2026, 7, 1, 0));
    }

    [Fact]
    public void Tolerance_is_five_minutes()
    {
        TrustedTimeEngine.Tolerance.Should().Be(TimeSpan.FromMinutes(5));
        TrustedTimeEngine.AnchorGraceDays.Should().Be(1);
    }
}
