using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Localization;
using SchoolManagement.Domain.Licensing;
using SchoolManagement.Infrastructure.Licensing;
using SchoolManagement.Infrastructure.Licensing.Ed25519;

namespace SchoolManagement.Tests.Licensing;

/// <summary>
/// Exercises the real activation flow (verifier, trusted clock, status
/// calculation, persistence order). Only the fingerprint source and the two
/// storage ports are fakes.
/// </summary>
public sealed class LicenseRepositoryTests
{
    private sealed class Harness
    {
        public Harness(
            DateTime? now = null,
            string? storedLicense = null,
            TrustedTimeRecord? storedTime = null,
            FakeFingerprintSource? fingerprint = null)
        {
            Clock = new FakeLocalClock(now ?? LicenseTestKit.Utc(2026, 6, 1));
            Storage = new FakeLicenseStorage { Text = storedLicense };
            TimeStorage = new FakeTrustedTimeStorage { Record = storedTime };
            Fingerprint = fingerprint ?? new FakeFingerprintSource();
            Repository = new LicenseRepository(
                Fingerprint,
                Storage,
                LicenseTestKit.Verifier(),
                new TrustedClockService(TimeStorage, Clock),
                NullLogger<LicenseRepository>.Instance);
        }

        public FakeLocalClock Clock { get; }

        public FakeLicenseStorage Storage { get; }

        public FakeTrustedTimeStorage TimeStorage { get; }

        public FakeFingerprintSource Fingerprint { get; }

        public LicenseRepository Repository { get; }

        public void SetNow(DateTime utc) => Clock.UtcNow = utc;
    }

    [Fact]
    public async Task No_stored_license_is_reported_as_unlicensed()
    {
        var harness = new Harness();

        var info = await harness.Repository.LoadAndVerifyAsync();

        info.Status.Should().Be(LicenseStatus.Unlicensed);
        info.FingerprintHash.Should().Be(LicenseTestKit.Fingerprint);
        info.RequiresActivation.Should().BeTrue();
        info.RawLicense.Should().BeEmpty();
    }

    [Fact]
    public async Task A_weak_fingerprint_is_surfaced_without_changing_the_status()
    {
        var harness = new Harness(fingerprint: new FakeFingerprintSource(isStrong: false, usedFallback: true));

        var info = await harness.Repository.LoadAndVerifyAsync();

        info.FingerprintIsStrong.Should().BeFalse();
        info.Status.Should().Be(LicenseStatus.Unlicensed);
    }

    [Fact]
    public async Task A_valid_license_is_active_well_before_expiry()
    {
        var harness = new Harness(storedLicense: LicenseTestKit.LicenseB);

        var info = await harness.Repository.LoadAndVerifyAsync();

        info.Status.Should().Be(LicenseStatus.Active);
        info.CanWrite.Should().BeTrue();
        info.RequiresActivation.Should().BeFalse();
        info.ExpiryDate.Should().Be(new DateOnly(2027, 1, 31));
    }

    [Fact]
    public async Task The_warning_window_is_reported_through_the_repository()
    {
        var harness = new Harness(
            now: LicenseTestKit.Utc(2027, 1, 15),
            storedLicense: LicenseTestKit.LicenseB);

        var info = await harness.Repository.LoadAndVerifyAsync();

        info.Status.Should().Be(LicenseStatus.ExpiringSoon);
        info.DaysRemaining.Should().Be(16);
    }

    [Fact]
    public async Task The_grace_window_allows_writing()
    {
        var harness = new Harness(
            now: LicenseTestKit.Utc(2027, 2, 5),
            storedLicense: LicenseTestKit.LicenseB);

        var info = await harness.Repository.LoadAndVerifyAsync();

        info.Status.Should().Be(LicenseStatus.GracePeriod);
        info.CanWrite.Should().BeTrue();
        info.GraceDaysLeft.Should().Be(26);
        info.ReadOnlyStartsOn.Should().Be(new DateOnly(2027, 3, 3));
    }

    [Fact]
    public async Task After_the_grace_window_the_shell_still_opens_but_writing_is_blocked()
    {
        var harness = new Harness(
            now: LicenseTestKit.Utc(2027, 3, 10),
            storedLicense: LicenseTestKit.LicenseB);

        var info = await harness.Repository.LoadAndVerifyAsync();

        info.Status.Should().Be(LicenseStatus.ReadOnly);
        info.CanWrite.Should().BeFalse();
        info.RequiresActivation.Should().BeFalse();
    }

    [Fact]
    public async Task A_not_yet_started_license_opens_the_shell_read_only()
    {
        var harness = new Harness(
            now: LicenseTestKit.Utc(2026, 1, 15),
            storedLicense: LicenseTestKit.LicenseA);

        var info = await harness.Repository.LoadAndVerifyAsync();

        info.Status.Should().Be(LicenseStatus.NotStarted);
        info.CanWrite.Should().BeFalse();
        info.RequiresActivation.Should().BeFalse();
        info.StartsOn.Should().Be(new DateOnly(2026, 2, 1));
    }

    [Fact]
    public async Task Rolling_the_clock_back_before_activation_is_detected()
    {
        var harness = new Harness(
            now: LicenseTestKit.Utc(2025, 12, 20),
            storedLicense: LicenseTestKit.LicenseB);

        var info = await harness.Repository.LoadAndVerifyAsync();

        info.ClockTampered.Should().BeTrue();
        // The trusted date is the issue anchor, not the rolled-back clock.
        info.Status.Should().Be(LicenseStatus.Active);
        harness.TimeStorage.Record.Should().NotBeNull();
        harness.TimeStorage.Record!.LastKnownUtc.Should().Be(LicenseTestKit.Utc(2026, 1, 1, 0));
    }

    [Fact]
    public async Task Rolling_the_clock_back_after_activation_uses_the_stored_instant()
    {
        var harness = new Harness(
            now: LicenseTestKit.Utc(2027, 1, 1),
            storedLicense: LicenseTestKit.LicenseB,
            storedTime: new TrustedTimeRecord(
                LicenseTestKit.Utc(2027, 2, 5),
                LicenseTestKit.Utc(2027, 2, 5),
                false));

        var info = await harness.Repository.LoadAndVerifyAsync();

        info.ClockTampered.Should().BeTrue();
        info.Status.Should().Be(LicenseStatus.GracePeriod);
        info.GraceDaysLeft.Should().Be(26);
    }

    [Fact]
    public async Task A_time_zone_change_is_not_mistaken_for_tampering()
    {
        var harness = new Harness(storedLicense: LicenseTestKit.LicenseB);
        harness.Clock.LocalNow = new DateTime(2026, 5, 31, 22, 0, 0, DateTimeKind.Unspecified);

        var info = await harness.Repository.LoadAndVerifyAsync();

        info.ClockTampered.Should().BeFalse();
        info.Status.Should().Be(LicenseStatus.Active);
    }

    [Fact]
    public async Task Garbage_in_storage_is_invalid_but_is_not_deleted()
    {
        var harness = new Harness(storedLicense: "not-a-license");

        var info = await harness.Repository.LoadAndVerifyAsync();

        info.Status.Should().Be(LicenseStatus.Invalid);
        info.FailureReason.Should().Be(LicenseFailureReason.Malformed);
        harness.Storage.Text.Should().Be("not-a-license");
        harness.Storage.Clears.Should().Be(0);
    }

    [Fact]
    public async Task A_license_for_another_device_is_invalid()
    {
        var harness = new Harness(
            storedLicense: LicenseTestKit.LicenseB,
            fingerprint: new FakeFingerprintSource(LicenseTestKit.OtherFingerprint));

        var info = await harness.Repository.LoadAndVerifyAsync();

        info.FailureReason.Should().Be(LicenseFailureReason.FingerprintMismatch);
        harness.Storage.Text.Should().Be(LicenseTestKit.LicenseB);
    }

    [Fact]
    public async Task The_fingerprint_is_read_once_and_cached()
    {
        var harness = new Harness(storedLicense: LicenseTestKit.LicenseB);

        await harness.Repository.LoadAndVerifyAsync();
        await harness.Repository.LoadAndVerifyAsync();

        harness.Fingerprint.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Activation_persists_the_license_and_the_trusted_time()
    {
        var harness = new Harness();

        var result = await harness.Repository.ActivateAsync(LicenseTestKit.LicenseB);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(LicenseStatus.Active);
        harness.Storage.Text.Should().Be(LicenseTestKit.LicenseB);
        harness.Storage.Writes.Should().Be(1);
        harness.TimeStorage.Writes.Should().Be(1);
        harness.TimeStorage.Record!.NoticedRollback.Should().BeFalse();
    }

    [Fact]
    public async Task Activation_trims_a_pasted_key_before_storing_it()
    {
        var harness = new Harness();

        var result = await harness.Repository.ActivateAsync($"\r\n  {LicenseTestKit.LicenseB}  \r\n");

        result.IsSuccess.Should().BeTrue();
        harness.Storage.Text.Should().Be(LicenseTestKit.LicenseB);
    }

    [Fact]
    public async Task Activation_of_a_valid_key_overwrites_a_previous_license()
    {
        var harness = new Harness(storedLicense: LicenseTestKit.LicenseA);

        var result = await harness.Repository.ActivateAsync(LicenseTestKit.LicenseB);

        result.IsSuccess.Should().BeTrue();
        harness.Storage.Text.Should().Be(LicenseTestKit.LicenseB);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_input_is_a_validation_failure(string text)
    {
        var harness = new Harness();

        var result = await harness.Repository.ActivateAsync(text);

        result.IsFailure.Should().BeTrue();
        result.Failure!.Kind.Should().Be(SchoolManagement.Application.Common.FailureKind.Validation);
        result.Failure.MessageKey.Should().Be(TranslationKeys.License.EnterKey);
        harness.Storage.Writes.Should().Be(0);
    }

    [Fact]
    public async Task A_tampered_key_is_rejected_with_the_exact_reason()
    {
        var harness = new Harness();
        LicenseEnvelope.TryDecode(LicenseTestKit.LicenseB, out var payload, out var signature)
            .Should().BeTrue();
        payload[10] ^= 0x01;
        var tampered = LicenseEnvelope.Encode(System.Text.Encoding.UTF8.GetString(payload), signature);

        var result = await harness.Repository.ActivateAsync(tampered);

        result.IsFailure.Should().BeTrue();
        result.Failure!.Kind.Should().Be(SchoolManagement.Application.Common.FailureKind.License);
        result.Failure.MessageKey.Should().Be(TranslationKeys.License.ErrorBadSignature);
        harness.Storage.Writes.Should().Be(0);
    }

    [Fact]
    public async Task A_key_for_another_device_is_rejected_with_the_mismatch_key()
    {
        var harness = new Harness(fingerprint: new FakeFingerprintSource(LicenseTestKit.OtherFingerprint));

        var result = await harness.Repository.ActivateAsync(LicenseTestKit.LicenseB);

        result.Failure!.MessageKey.Should().Be(TranslationKeys.License.ErrorFingerprintMismatch);
        harness.Storage.Writes.Should().Be(0);
    }

    [Fact]
    public async Task A_newer_version_is_rejected_with_the_update_key()
    {
        var harness = new Harness();

        var result = await harness.Repository.ActivateAsync(LicenseTestKit.LicenseVersion3);

        result.Failure!.MessageKey.Should().Be(TranslationKeys.License.ErrorUnsupportedVersion);
        harness.Storage.Writes.Should().Be(0);
    }

    [Fact]
    public async Task Garbage_input_is_rejected_as_malformed()
    {
        var harness = new Harness();

        var result = await harness.Repository.ActivateAsync("garbage");

        result.Failure!.MessageKey.Should().Be(TranslationKeys.License.ErrorMalformed);
        harness.Storage.Writes.Should().Be(0);
    }

    [Fact]
    public async Task A_rejected_activation_never_deletes_or_replaces_the_existing_license()
    {
        var harness = new Harness(storedLicense: LicenseTestKit.LicenseB);

        var result = await harness.Repository.ActivateAsync("garbage");

        result.IsFailure.Should().BeTrue();
        harness.Storage.Text.Should().Be(LicenseTestKit.LicenseB);
        harness.Storage.Clears.Should().Be(0);
        harness.Storage.Writes.Should().Be(0);
    }

    [Fact]
    public async Task Activating_while_the_clock_is_rolled_back_records_a_tamper_flag()
    {
        var harness = new Harness(now: LicenseTestKit.Utc(2025, 12, 20));

        var result = await harness.Repository.ActivateAsync(LicenseTestKit.LicenseB);

        result.IsSuccess.Should().BeTrue();
        result.Value.ClockTampered.Should().BeTrue();
        harness.TimeStorage.Record!.NoticedRollback.Should().BeTrue();
        harness.TimeStorage.Record.LastKnownUtc.Should().Be(LicenseTestKit.Utc(2026, 1, 1, 0));
    }

    [Fact]
    public async Task Loading_after_activation_returns_the_same_status()
    {
        var harness = new Harness();
        var activated = await harness.Repository.ActivateAsync(LicenseTestKit.LicenseB);

        var loaded = await harness.Repository.LoadAndVerifyAsync();

        loaded.Status.Should().Be(activated.Value.Status);
        loaded.ExpiryDate.Should().Be(activated.Value.ExpiryDate);
    }

    [Fact]
    public async Task Activation_is_cancellable()
    {
        var harness = new Harness();
        using var source = new CancellationTokenSource();
        source.Cancel();

        var act = () => harness.Repository.ActivateAsync(LicenseTestKit.LicenseB, source.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        harness.Storage.Writes.Should().Be(0);
    }

    [Fact]
    public async Task Load_is_cancellable()
    {
        var harness = new Harness(storedLicense: LicenseTestKit.LicenseB);
        using var source = new CancellationTokenSource();
        source.Cancel();

        var act = () => harness.Repository.LoadAndVerifyAsync(source.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Clearing_removes_the_stored_license()
    {
        var harness = new Harness(storedLicense: LicenseTestKit.LicenseB);

        await harness.Repository.ClearAsync();

        harness.Storage.Text.Should().BeNull();
        harness.Storage.Clears.Should().Be(1);

        var info = await harness.Repository.LoadAndVerifyAsync();
        info.Status.Should().Be(LicenseStatus.Unlicensed);
    }

    [Fact]
    public async Task A_repository_never_throws_for_an_unexpected_storage_failure()
    {
        var throwing = new ThrowingLicenseStorage();

        var repository = new LicenseRepository(
            new FakeFingerprintSource(),
            throwing,
            LicenseTestKit.Verifier(),
            new TrustedClockService(new FakeTrustedTimeStorage(), new FakeLocalClock(LicenseTestKit.Utc(2026, 6, 1))),
            NullLogger<LicenseRepository>.Instance);

        var info = await repository.LoadAndVerifyAsync();

        info.Status.Should().Be(LicenseStatus.Invalid);
        info.FailureReason.Should().Be(LicenseFailureReason.Malformed);
    }

    private sealed class ThrowingLicenseStorage : ILicenseStorage
    {
        public string? Read() => throw new IOException("disk on fire");

        public void Write(string licenseText) => throw new IOException("disk on fire");

        public void Clear() => throw new IOException("disk on fire");
    }
}
