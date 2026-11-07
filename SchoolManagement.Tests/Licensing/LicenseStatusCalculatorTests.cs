using FluentAssertions;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Localization;
using SchoolManagement.Domain.Licensing;

namespace SchoolManagement.Tests.Licensing;

public sealed class LicenseStatusCalculatorTests
{
    private static readonly DateOnly Expiry = new(2026, 12, 31);

    private static LicensePayload Payload(DateOnly? notBefore = null) =>
        new(2, LicenseTestKit.Fingerprint, Expiry, "School", new DateOnly(2025, 1, 1), notBefore, "k1");

    private static LicenseInfo At(
        int year,
        int month,
        int day,
        DateOnly? notBefore = null,
        DateOnly? expiry = null) =>
        LicenseStatusCalculator.Calculate(
            new LicensePayload(
                2,
                LicenseTestKit.Fingerprint,
                expiry ?? Expiry,
                "School",
                new DateOnly(2025, 1, 1),
                notBefore,
                "k1"),
            "raw",
            LicenseTestKit.Utc(year, month, day),
            clockTampered: false);

    [Fact]
    public void Thirty_one_days_before_expiry_is_active() =>
        At(2026, 11, 30).Status.Should().Be(LicenseStatus.Active);

    [Fact]
    public void Thirty_days_before_expiry_is_expiring_soon() =>
        At(2026, 12, 1).Status.Should().Be(LicenseStatus.ExpiringSoon);

    [Fact]
    public void The_whole_warning_window_reports_the_exact_days()
    {
        At(2026, 12, 1).DaysRemaining.Should().Be(30);
        At(2026, 12, 15).DaysRemaining.Should().Be(16);
        At(2026, 12, 31).DaysRemaining.Should().Be(0);
    }

    [Fact]
    public void The_expiry_day_is_still_not_expired() =>
        At(2026, 12, 31).Status.Should().Be(LicenseStatus.ExpiringSoon);

    [Fact]
    public void The_day_after_expiry_enters_grace_with_thirty_days_left()
    {
        var info = At(2027, 1, 1);

        info.Status.Should().Be(LicenseStatus.GracePeriod);
        info.GraceDaysLeft.Should().Be(30);
        info.DaysRemaining.Should().Be(0);
        info.ReadOnlyStartsOn.Should().Be(new DateOnly(2027, 1, 31));
    }

    [Fact]
    public void The_tenth_grace_day_reports_twenty_one_days_left()
    {
        var info = At(2027, 1, 10);

        info.Status.Should().Be(LicenseStatus.GracePeriod);
        info.GraceDaysLeft.Should().Be(21);
    }

    [Fact]
    public void The_last_grace_day_reports_one_day_left()
    {
        var info = At(2027, 1, 30);

        info.Status.Should().Be(LicenseStatus.GracePeriod);
        info.GraceDaysLeft.Should().Be(1);
        info.ReadOnlyStartsOn.Should().Be(new DateOnly(2027, 1, 31));
    }

    [Fact]
    public void The_day_after_the_grace_window_is_read_only()
    {
        var info = At(2027, 1, 31);

        info.Status.Should().Be(LicenseStatus.ReadOnly);
        info.GraceDaysLeft.Should().Be(0);
        info.ReadOnlyStartsOn.Should().Be(new DateOnly(2027, 1, 31));
    }

    [Fact]
    public void Read_only_lasts_indefinitely()
    {
        At(2029, 5, 5).Status.Should().Be(LicenseStatus.ReadOnly);
        At(2100, 1, 1).Status.Should().Be(LicenseStatus.ReadOnly);
    }

    [Fact]
    public void The_not_before_day_itself_is_active()
    {
        var info = At(
            2027,
            1,
            10,
            notBefore: new DateOnly(2027, 1, 10),
            expiry: new DateOnly(2027, 6, 30));

        info.Status.Should().Be(LicenseStatus.Active);
        info.StartsOn.Should().Be(new DateOnly(2027, 1, 10));
    }

    [Fact]
    public void The_day_before_not_before_is_not_started()
    {
        var info = At(
            2027,
            1,
            9,
            notBefore: new DateOnly(2027, 1, 10),
            expiry: new DateOnly(2027, 6, 30));

        info.Status.Should().Be(LicenseStatus.NotStarted);
        info.DaysRemaining.Should().Be(1);
        info.StartsOn.Should().Be(new DateOnly(2027, 1, 10));
    }

    [Fact]
    public void Not_started_counts_days_until_the_start()
    {
        var info = LicenseStatusCalculator.Calculate(
            new LicensePayload(2, LicenseTestKit.Fingerprint, new DateOnly(2027, 6, 30), "School",
                new DateOnly(2025, 1, 1), new DateOnly(2027, 3, 1), "k1"),
            "raw",
            LicenseTestKit.Utc(2027, 2, 20),
            clockTampered: false);

        info.Status.Should().Be(LicenseStatus.NotStarted);
        info.DaysRemaining.Should().Be(9);
    }

    [Fact]
    public void Not_started_wins_over_expiring_soon()
    {
        // nbf and exp are the same day: before it, the license has not started.
        var info = At(2026, 12, 30, notBefore: new DateOnly(2026, 12, 31));
        info.Status.Should().Be(LicenseStatus.NotStarted);
    }

    [Fact]
    public void Every_status_except_the_warning_and_active_states_blocks_writing()
    {
        At(2026, 6, 1).CanWrite.Should().BeTrue();               // Active
        At(2026, 12, 15).CanWrite.Should().BeTrue();             // ExpiringSoon
        At(2027, 1, 10).CanWrite.Should().BeTrue();              // GracePeriod
        At(2027, 2, 10).CanWrite.Should().BeFalse();             // ReadOnly
        At(2027, 1, 9, new DateOnly(2027, 1, 10), new DateOnly(2027, 6, 30)).CanWrite.Should().BeFalse(); // NotStarted
        LicenseInfo.Unlicensed("f").CanWrite.Should().BeFalse();
        LicenseInfo.Invalid(LicenseFailureReason.BadSignature, "f").CanWrite.Should().BeFalse();
    }

    [Fact]
    public void Only_unlicensed_and_invalid_require_the_activation_screen()
    {
        At(2026, 6, 1).RequiresActivation.Should().BeFalse();
        At(2026, 12, 15).RequiresActivation.Should().BeFalse();
        At(2027, 1, 10).RequiresActivation.Should().BeFalse();
        At(2027, 2, 10).RequiresActivation.Should().BeFalse();
        At(2027, 1, 9, new DateOnly(2027, 1, 10), new DateOnly(2027, 6, 30)).RequiresActivation.Should().BeFalse();
        LicenseInfo.Unlicensed("f").RequiresActivation.Should().BeTrue();
        LicenseInfo.Invalid(LicenseFailureReason.BadSignature, "f").RequiresActivation.Should().BeTrue();
    }

    [Fact]
    public void Clock_tampering_is_carried_through_every_state()
    {
        var payload = Payload();

        foreach (var now in new[]
                 {
                     LicenseTestKit.Utc(2026, 6, 1),
                     LicenseTestKit.Utc(2026, 12, 15),
                     LicenseTestKit.Utc(2027, 1, 10),
                     LicenseTestKit.Utc(2027, 2, 10),
                 })
        {
            LicenseStatusCalculator.Calculate(payload, "raw", now, clockTampered: true)
                .ClockTampered.Should().BeTrue();
        }
    }

    [Fact]
    public void The_raw_license_and_payload_fields_are_preserved()
    {
        var info = At(2026, 6, 1);

        info.RawLicense.Should().Be("raw");
        info.FingerprintHash.Should().Be(LicenseTestKit.Fingerprint);
        info.LicensedTo.Should().Be("School");
        info.ExpiryDate.Should().Be(Expiry);
    }

    [Fact]
    public void The_seven_states_are_all_modelled()
    {
        Enum.GetValues<LicenseStatus>().Should().HaveCount(7);
        Enum.GetValues<LicenseStatus>().Should().BeEquivalentTo(new[]
        {
            LicenseStatus.Active,
            LicenseStatus.ExpiringSoon,
            LicenseStatus.GracePeriod,
            LicenseStatus.ReadOnly,
            LicenseStatus.NotStarted,
            LicenseStatus.Unlicensed,
            LicenseStatus.Invalid,
        });
    }

    [Fact]
    public void A_weak_fingerprint_flag_is_propagated()
    {
        var info = LicenseStatusCalculator.Calculate(
            Payload(), "raw", LicenseTestKit.Utc(2026, 6, 1), clockTampered: false,
            fingerprintIsStrong: false);

        info.FingerprintIsStrong.Should().BeFalse();
    }
}

public sealed class LicenseInfoTests
{
    private static LicenseInfo Info(
        LicenseStatus status,
        DateOnly? expiry = null,
        int daysRemaining = 0,
        bool clockTampered = false,
        int? graceDaysLeft = null,
        DateOnly? readOnlyStartsOn = null,
        DateOnly? startsOn = null) =>
        new(status, LicenseTestKit.Fingerprint, expiry, daysRemaining, "School", "raw", null,
            clockTampered, graceDaysLeft, readOnlyStartsOn, startsOn);

    [Theory]
    [InlineData(LicenseStatus.Active, TranslationKeys.License.StatusActive)]
    [InlineData(LicenseStatus.ExpiringSoon, TranslationKeys.License.StatusExpiringSoon)]
    [InlineData(LicenseStatus.GracePeriod, TranslationKeys.License.StatusGracePeriod)]
    [InlineData(LicenseStatus.ReadOnly, TranslationKeys.License.StatusReadOnly)]
    [InlineData(LicenseStatus.NotStarted, TranslationKeys.License.StatusNotStarted)]
    [InlineData(LicenseStatus.Unlicensed, TranslationKeys.License.StatusUnlicensed)]
    [InlineData(LicenseStatus.Invalid, TranslationKeys.License.StatusInvalid)]
    public void Each_state_maps_to_its_translation_key(LicenseStatus status, string key)
    {
        Info(status).StatusMessageKey.Should().Be(key);
    }

    [Fact]
    public void Expiring_soon_banner_uses_the_days_parameter()
    {
        var info = Info(LicenseStatus.ExpiringSoon, new DateOnly(2027, 1, 10), daysRemaining: 12);

        info.ShouldWarn.Should().BeTrue();
        info.BannerMessageKey.Should().Be(TranslationKeys.License.WarningExpiringSoon);
        info.BannerParameters["days"].Should().Be("12");
    }

    [Fact]
    public void Grace_banner_is_honest_about_days_left_and_the_read_only_date()
    {
        var info = Info(
            LicenseStatus.GracePeriod,
            new DateOnly(2026, 12, 31),
            graceDaysLeft: 21,
            readOnlyStartsOn: new DateOnly(2027, 1, 31));

        info.BannerMessageKey.Should().Be(TranslationKeys.License.WarningGracePeriod);
        info.BannerParameters["days"].Should().Be("10");
        info.BannerParameters["graceLeft"].Should().Be("21");
        info.BannerParameters["date"].Should().Be("2027-01-31");
        info.GracePeriodDetailKey.Should().Be(TranslationKeys.License.GracePeriodDetail);
    }

    [Fact]
    public void First_grace_day_has_thirty_days_left_and_reports_one_day_past_expiry()
    {
        var info = Info(
            LicenseStatus.GracePeriod,
            new DateOnly(2026, 12, 31),
            graceDaysLeft: 30,
            readOnlyStartsOn: new DateOnly(2027, 1, 31));

        info.BannerParameters["days"].Should().Be("1");
        info.BannerParameters["graceLeft"].Should().Be("30");
    }

    [Fact]
    public void Read_only_banner_does_not_promise_writing()
    {
        var info = Info(
            LicenseStatus.ReadOnly,
            new DateOnly(2026, 12, 31),
            readOnlyStartsOn: new DateOnly(2027, 1, 31));

        info.BannerMessageKey.Should().Be(TranslationKeys.License.WarningReadOnly);
        info.ShouldWarn.Should().BeTrue();
        info.CanWrite.Should().BeFalse();
    }

    [Fact]
    public void Not_started_banner_shows_the_start_date()
    {
        var info = Info(LicenseStatus.NotStarted, startsOn: new DateOnly(2027, 3, 1));

        info.BannerMessageKey.Should().Be(TranslationKeys.License.StartsOn);
        info.BannerParameters["date"].Should().Be("2027-03-01");
    }

    [Fact]
    public void Active_has_no_banner()
    {
        var info = Info(LicenseStatus.Active, new DateOnly(2027, 12, 31), daysRemaining: 300);

        info.ShouldWarn.Should().BeFalse();
        info.BannerMessageKey.Should().BeNull();
        info.BannerTitleKey.Should().BeNull();
        info.BannerParameters.Should().BeEmpty();
    }

    [Fact]
    public void Clock_tampering_produces_a_danger_banner_with_a_title()
    {
        var info = Info(LicenseStatus.Active, clockTampered: true);

        info.ShouldWarn.Should().BeTrue();
        info.BannerTitleKey.Should().Be(TranslationKeys.License.ClockTamperTitle);
        info.BannerMessageKey.Should().Be(TranslationKeys.License.ClockTamperBody);
    }

    [Fact]
    public void Invalid_exposes_the_mapped_failure_message()
    {
        var info = LicenseInfo.Invalid(LicenseFailureReason.FingerprintMismatch, "f");

        info.StatusMessageKey.Should().Be(TranslationKeys.License.StatusInvalid);
        info.FailureMessageKey.Should().Be(TranslationKeys.License.ErrorFingerprintMismatch);
        info.RequiresActivation.Should().BeTrue();
    }

    [Fact]
    public void Unlicensed_is_not_a_failure_and_does_not_warn()
    {
        var info = LicenseInfo.Unlicensed("f");

        info.FailureMessageKey.Should().BeNull();
        info.ShouldWarn.Should().BeFalse();
        info.Status.Should().Be(LicenseStatus.Unlicensed);
    }

    [Fact]
    public void Default_values_are_safe()
    {
        var info = new LicenseInfo(LicenseStatus.Active, LicenseTestKit.Fingerprint);

        info.DaysRemaining.Should().Be(0);
        info.RawLicense.Should().BeEmpty();
        info.ClockTampered.Should().BeFalse();
        info.FingerprintIsStrong.Should().BeTrue();
    }
}

public sealed class LicenseFailureMapperTests
{
    [Theory]
    [InlineData(LicenseFailureReason.Malformed, TranslationKeys.License.ErrorMalformed)]
    [InlineData(LicenseFailureReason.BadSignature, TranslationKeys.License.ErrorBadSignature)]
    [InlineData(LicenseFailureReason.FingerprintMismatch, TranslationKeys.License.ErrorFingerprintMismatch)]
    [InlineData(LicenseFailureReason.UnsupportedVersion, TranslationKeys.License.ErrorUnsupportedVersion)]
    [InlineData(LicenseFailureReason.NotConfigured, TranslationKeys.License.ErrorNotConfigured)]
    public void Every_reason_maps_to_a_distinct_key(LicenseFailureReason reason, string key)
    {
        LicenseFailureMapper.ToTranslationKey(reason).Should().Be(key);
    }

    [Fact]
    public void Every_enum_member_is_mapped()
    {
        var keys = Enum.GetValues<LicenseFailureReason>()
            .Select(LicenseFailureMapper.ToTranslationKey)
            .ToArray();

        keys.Should().OnlyHaveUniqueItems();
        keys.Should().OnlyContain(key => !string.IsNullOrWhiteSpace(key));
    }
}
