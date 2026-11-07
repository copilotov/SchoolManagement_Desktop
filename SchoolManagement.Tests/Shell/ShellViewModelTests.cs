using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Groups;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Settings;
using SchoolManagement.Application.Students;
using SchoolManagement.Application.UseCases;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.ViewModels;
using SchoolManagement.Infrastructure.Localization;

namespace SchoolManagement.Tests.Shell;

public sealed class ShellViewModelTests
{
    [Fact]
    public async Task Language_switch_updates_direction_and_navigation_labels_immediately()
    {
        var localization = CreateLocalization();

        using var provider = CreateNavigationProvider();
        using var shell = CreateShell(provider, localization);

        shell.FlowDirection.Should().Be(FlowDirection.RightToLeft);

        // Phase D2: 9 destinations were requested plus the Phase 4 Academics
        // screen, which must stay reachable from the rail.
        shell.Items.Should().HaveCount(10);
        shell.Items.Should().Contain(item => item.Tab == ShellTab.Attendance);
        shell.Items.Should().Contain(item => item.Tab == ShellTab.Homework);
        shell.Items.Should().Contain(item => item.Tab == ShellTab.Notifications);
        shell.Items.Should().Contain(item => item.Tab == ShellTab.Messages);
        shell.Items.Should().Contain(item => item.Tab == ShellTab.Academics);
        shell.SelectedItem.Should().NotBeNull();
        shell.SelectedItem!.Tab.Should().Be(ShellTab.Dashboard);
        shell.CurrentViewModel.Should().BeOfType<DashboardViewModel>();

        (await localization.SetLanguageAsync("en")).Should().BeTrue();

        shell.FlowDirection.Should().Be(FlowDirection.LeftToRight);
        shell.Items.Single(item => item.Tab == ShellTab.Students).Label.Should().Be("Students");
        shell.Items.Single(item => item.Tab == ShellTab.Academics).Label.Should().Be("Academics");
        shell.Items.Single(item => item.Tab == ShellTab.Settings).Label.Should().Be("Settings");
        shell.Items.Single(item => item.Tab == ShellTab.Notifications).Label.Should().Be("Notifications");
        shell.LanguageBadge.Should().Be("EN");
    }

    [Fact]
    public async Task Rail_groups_keep_the_documented_order_in_every_language()
    {
        var localization = CreateLocalization();
        using var provider = CreateNavigationProvider();
        using var shell = CreateShell(provider, localization);

        shell.Groups.Select(group => group.Key).Should().Equal(
            TranslationKeys.Nav.GroupHome,
            TranslationKeys.Nav.GroupAcademic,
            TranslationKeys.Nav.GroupFinance,
            TranslationKeys.Nav.GroupCommunication,
            TranslationKeys.Nav.GroupSystem);

        shell.Groups.Select(group => group.Items.Count).Should().Equal(1, 5, 1, 2, 1);

        // The group order must not depend on the language, only the captions do.
        (await localization.SetLanguageAsync("fr")).Should().BeTrue();

        shell.Groups.Select(group => group.Key).Should().Equal(
            TranslationKeys.Nav.GroupHome,
            TranslationKeys.Nav.GroupAcademic,
            TranslationKeys.Nav.GroupFinance,
            TranslationKeys.Nav.GroupCommunication,
            TranslationKeys.Nav.GroupSystem);
        shell.Groups[1].Label.Should().Be("Scolarité");
    }

    [Fact]
    public void The_rail_collapses_below_1200_and_expands_at_the_threshold()
    {
        var localization = CreateLocalization();
        using var provider = CreateNavigationProvider();
        using var shell = CreateShell(provider, localization);

        shell.IsNavCollapsed.Should().BeFalse();
        shell.NavWidth.Should().Be(ShellViewModel.ExpandedNavWidth);

        shell.UpdateWindowWidth(ShellViewModel.AutoCollapseWidth - 1);

        shell.IsNavCollapsed.Should().BeTrue();
        shell.NavWidth.Should().Be(ShellViewModel.CompactNavWidth);
        shell.NavColumnWidth.Value.Should().Be(ShellViewModel.CompactNavWidth);

        shell.UpdateWindowWidth(ShellViewModel.AutoCollapseWidth);

        shell.IsNavCollapsed.Should().BeFalse();
        shell.NavWidth.Should().Be(ShellViewModel.ExpandedNavWidth);
    }

    [Fact]
    public void Toggling_after_an_automatic_collapse_expands_on_the_first_click()
    {
        var localization = CreateLocalization();
        using var provider = CreateNavigationProvider();
        using var shell = CreateShell(provider, localization);

        shell.UpdateWindowWidth(1100);
        shell.IsNavCollapsed.Should().BeTrue();

        shell.ToggleNavCommand.Execute(null);

        shell.IsNavCollapsed.Should().BeFalse();
        shell.IsNavExpanded.Should().BeTrue();

        shell.ToggleNavCommand.Execute(null);

        shell.IsNavCollapsed.Should().BeTrue();
        shell.IsNavExpanded.Should().BeFalse();
    }

    [Fact]
    public void Shortcuts_are_translated_into_shell_actions()
    {
        var localization = CreateLocalization();
        var navigation = Substitute.For<INavigationService>();
        using var shell = CreateShell(navigation, localization);

        shell.CommandPalette.IsOpen.Should().BeFalse();
        shell.ExecuteShortcut(ShortcutAction.CommandPalette);
        shell.CommandPalette.IsOpen.Should().BeTrue();

        shell.ExecuteShortcut(ShortcutAction.Close);
        shell.CommandPalette.IsOpen.Should().BeFalse();

        // Ctrl+F is an alias of the global search: it opens the same palette.
        shell.ExecuteShortcut(ShortcutAction.Search);
        shell.CommandPalette.IsOpen.Should().BeTrue();
        shell.ExecuteShortcut(ShortcutAction.Close);

        shell.ExecuteShortcut(ShortcutAction.Navigate, ShellTab.Settings);
        navigation.Received(1).NavigateTo(ShellTab.Settings);

        var helpRaised = false;
        shell.ShortcutsRequested += (_, _) => helpRaised = true;
        shell.ExecuteShortcut(ShortcutAction.Help);
        helpRaised.Should().BeTrue();

        var collapsed = shell.IsNavCollapsed;
        shell.ExecuteShortcut(ShortcutAction.ToggleNav);
        shell.IsNavCollapsed.Should().NotBe(collapsed);

        // Save and print are page-owned: the shell must never run them itself.
        navigation.ClearReceivedCalls();
        shell.ExecuteShortcut(ShortcutAction.Save);
        shell.ExecuteShortcut(ShortcutAction.Print);
        navigation.DidNotReceive().NavigateTo(Arg.Any<ShellTab>());
    }

    [Fact]
    public async Task Unread_count_drives_the_notification_badge()
    {
        var localization = CreateLocalization();
        var notifications = Substitute.For<INotificationRepository>();
        notifications
            .CountUnreadAsync(Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(12));

        using var provider = CreateNavigationProvider();
        using var shell = CreateShell(provider, localization, notifications);

        await shell.RefreshUnreadAsync();

        shell.UnreadCount.Should().Be(12);
        shell.HasUnread.Should().BeTrue();

        var item = shell.Items.Single(entry => entry.Tab == ShellTab.Notifications);
        item.HasBadge.Should().BeTrue();
        item.BadgeText.Should().Be("12");
    }

    [Fact]
    public void A_sub_page_exposes_the_back_action_and_the_breadcrumb()
    {
        var localization = CreateLocalization();
        using var provider = CreateNavigationProvider();
        using var shell = CreateShell(provider, localization);

        shell.CanGoBack.Should().BeFalse();
        shell.CurrentPageTitle.Should().Be(localization.Tr(TranslationKeys.Menu.Dashboard));
        shell.CurrentGroupLabel.Should().Be(localization.Tr(TranslationKeys.Nav.GroupHome));
    }

    internal static LocalizationService CreateLocalization()
    {
        var localization = new LocalizationService(
            FindTranslationsDirectory(),
            NullLogger<LocalizationService>.Instance);

        localization.InitializeAsync("ar").GetAwaiter().GetResult().Should().BeTrue();
        return localization;
    }

    internal static ServiceProvider CreateNavigationProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddTransient<DashboardViewModel>();
        return services.BuildServiceProvider();
    }

    internal static ShellViewModel CreateShell(
        ServiceProvider provider,
        ILocalizationService localization,
        INotificationRepository? notifications = null,
        IReferenceDataRepository? referenceData = null,
        IThemeService? themeService = null)
        => CreateShell(
            provider.GetRequiredService<INavigationService>(),
            localization,
            notifications,
            referenceData,
            themeService);

    internal static ShellViewModel CreateShell(
        INavigationService navigation,
        ILocalizationService localization,
        INotificationRepository? notifications = null,
        IReferenceDataRepository? referenceData = null,
        IThemeService? themeService = null)
    {
        var licenseService = Substitute.For<ILicenseService>();
        licenseService
            .LoadAndVerifyAsync(Arg.Any<CancellationToken>())
            .Returns(LicenseInfo.Unlicensed("fingerprint"));

        var license = new LicenseViewModel(
            licenseService,
            Substitute.For<IActivationDialogService>(),
            localization);

        var settingsStore = Substitute.For<IAppSettingsStore>();
        settingsStore
            .SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var notificationRepository = notifications ?? Substitute.For<INotificationRepository>();
        if (notifications is null)
        {
            notificationRepository
                .CountUnreadAsync(Arg.Any<CancellationToken>())
                .Returns(Result<int>.Success(0));
        }

        var reference = referenceData ?? Substitute.For<IReferenceDataRepository>();
        reference
            .GetSchoolNameAsync(Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("Test School"));

        var theme = themeService ?? Substitute.For<IThemeService>();
        theme.CurrentTheme.Returns(AppTheme.Light);

        var palette = new CommandPaletteViewModel(
            navigation,
            localization,
            new GetStudentsUseCase(Substitute.For<IStudentRepository>()),
            new GetGroupsUseCase(Substitute.For<IGroupRepository>()));

        return new ShellViewModel(
            navigation,
            localization,
            license,
            theme,
            settingsStore,
            notificationRepository,
            reference,
            Substitute.For<IToastService>(),
            palette);
    }

    private static string FindTranslationsDirectory([CallerFilePath] string callerPath = "")
    {
        var output = Path.Combine(AppContext.BaseDirectory, "Resources", "Translations");
        if (File.Exists(Path.Combine(output, "ar.json")))
        {
            return output;
        }

        var testsDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(callerPath)!, ".."));
        var root = Path.GetFullPath(Path.Combine(testsDirectory, ".."));
        return Path.Combine(root, "SchoolManagement.Desktop", "Resources", "Translations");
    }
}
