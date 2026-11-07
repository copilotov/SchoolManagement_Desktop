using System.Windows;
using FluentAssertions;
using NSubstitute;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Localization;
using SchoolManagement.Desktop.ViewModels;
using SchoolManagement.Domain.Licensing;
using SchoolManagement.Tests.Shell;

namespace SchoolManagement.Tests.Shell;

public sealed class LicenseBannerViewModelTests
{
    [Fact]
    public void An_active_license_shows_no_banner()
    {
        var localization = ShellViewModelTests.CreateLocalization();
        using var viewModel = CreateViewModel(localization);

        viewModel.Info = Active();

        viewModel.HasBanner.Should().BeFalse();
        viewModel.BannerVisibility.Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public void Expiring_soon_shows_an_informational_banner_that_can_be_dismissed()
    {
        var localization = ShellViewModelTests.CreateLocalization();
        using var viewModel = CreateViewModel(localization);

        viewModel.Info = new LicenseInfo(
            LicenseStatus.ExpiringSoon,
            "fingerprint",
            ExpiryDate: new DateOnly(2026, 12, 31),
            DaysRemaining: 12);

        viewModel.HasBanner.Should().BeTrue();
        viewModel.Severity.Should().Be("Info");
        viewModel.CanDismiss.Should().BeTrue();
        viewModel.DismissVisibility.Should().Be(Visibility.Visible);
        viewModel.BannerMessage.Should().Contain("12");

        viewModel.DismissCommand.Execute(null);

        viewModel.HasBanner.Should().BeFalse();
        viewModel.BannerVisibility.Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public void Grace_period_uses_the_warning_severity()
    {
        var localization = ShellViewModelTests.CreateLocalization();
        using var viewModel = CreateViewModel(localization);

        viewModel.Info = new LicenseInfo(
            LicenseStatus.GracePeriod,
            "fingerprint",
            ExpiryDate: new DateOnly(2026, 10, 1),
            GraceDaysLeft: 20,
            ReadOnlyStartsOn: new DateOnly(2026, 10, 31));

        viewModel.Severity.Should().Be("Warning");
        viewModel.HasBanner.Should().BeTrue();
        viewModel.CanDismiss.Should().BeTrue();
    }

    [Fact]
    public void Read_only_cannot_be_dismissed_and_uses_the_danger_severity()
    {
        var localization = ShellViewModelTests.CreateLocalization();
        using var viewModel = CreateViewModel(localization);

        viewModel.Info = new LicenseInfo(
            LicenseStatus.ReadOnly,
            "fingerprint",
            ExpiryDate: new DateOnly(2026, 9, 1));

        viewModel.HasBanner.Should().BeTrue();
        viewModel.Severity.Should().Be("Danger");
        viewModel.CanDismiss.Should().BeFalse();
        viewModel.DismissVisibility.Should().Be(Visibility.Collapsed);

        viewModel.DismissCommand.Execute(null);

        viewModel.HasBanner.Should().BeTrue();
        viewModel.BannerVisibility.Should().Be(Visibility.Visible);
    }

    [Fact]
    public void A_clock_tamper_wins_over_the_license_state()
    {
        var localization = ShellViewModelTests.CreateLocalization();
        using var viewModel = CreateViewModel(localization);

        viewModel.Info = new LicenseInfo(
            LicenseStatus.Active,
            "fingerprint",
            ExpiryDate: new DateOnly(2027, 1, 1),
            DaysRemaining: 300,
            ClockTampered: true);

        viewModel.HasBanner.Should().BeTrue();
        viewModel.HasBannerTitle.Should().BeTrue();
        viewModel.BannerTitleVisibility.Should().Be(Visibility.Visible);
        viewModel.Severity.Should().Be("Danger");
    }

    [Fact]
    public void A_new_license_state_restores_a_dismissed_banner()
    {
        var localization = ShellViewModelTests.CreateLocalization();
        using var viewModel = CreateViewModel(localization);

        viewModel.Info = new LicenseInfo(
            LicenseStatus.ExpiringSoon,
            "fingerprint",
            ExpiryDate: new DateOnly(2026, 12, 31),
            DaysRemaining: 12);
        viewModel.DismissCommand.Execute(null);
        viewModel.HasBanner.Should().BeFalse();

        viewModel.Info = new LicenseInfo(
            LicenseStatus.GracePeriod,
            "fingerprint",
            ExpiryDate: new DateOnly(2026, 10, 1),
            GraceDaysLeft: 3,
            ReadOnlyStartsOn: new DateOnly(2026, 10, 31));

        viewModel.HasBanner.Should().BeTrue();
    }

    [Fact]
    public async Task The_banner_texts_follow_a_language_switch()
    {
        var localization = ShellViewModelTests.CreateLocalization();
        using var viewModel = CreateViewModel(localization);

        viewModel.Info = new LicenseInfo(
            LicenseStatus.GracePeriod,
            "fingerprint",
            ExpiryDate: new DateOnly(2026, 10, 1),
            GraceDaysLeft: 20,
            ReadOnlyStartsOn: new DateOnly(2026, 10, 31));

        var arabic = viewModel.BannerMessage;
        viewModel.DismissText.Should().NotBeNullOrWhiteSpace();

        (await localization.SetLanguageAsync("en")).Should().BeTrue();

        viewModel.BannerMessage.Should().NotBe(arabic);
        viewModel.DismissText.Should().Be("Dismiss");
    }

    private static LicenseViewModel CreateViewModel(ILocalizationService localization) =>
        new(
            Substitute.For<ILicenseService>(),
            Substitute.For<IActivationDialogService>(),
            localization);

    private static LicenseInfo Active() => new(
        LicenseStatus.Active,
        "fingerprint",
        ExpiryDate: new DateOnly(2027, 6, 30),
        DaysRemaining: 400);
}
