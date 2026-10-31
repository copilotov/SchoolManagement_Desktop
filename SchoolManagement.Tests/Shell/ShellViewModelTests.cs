using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.ViewModels;
using SchoolManagement.Infrastructure.Localization;

namespace SchoolManagement.Tests.Shell;

public sealed class ShellViewModelTests
{
    [Fact]
    public async Task Language_switch_updates_direction_and_navigation_labels_immediately()
    {
        var localization = new LocalizationService(
            FindTranslationsDirectory(),
            NullLogger<LocalizationService>.Instance);

        (await localization.InitializeAsync("ar")).Should().BeTrue();

        var services = new ServiceCollection();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddTransient<DashboardViewModel>();
        using var provider = services.BuildServiceProvider();

        using var shell = new ShellViewModel(
            provider.GetRequiredService<INavigationService>(),
            localization);

        shell.FlowDirection.Should().Be(FlowDirection.RightToLeft);
        shell.Items.Should().HaveCount(5);
        shell.SelectedItem.Should().NotBeNull();
        shell.SelectedItem!.Tab.Should().Be(ShellTab.Dashboard);
        shell.CurrentViewModel.Should().BeOfType<DashboardViewModel>();

        (await localization.SetLanguageAsync("en")).Should().BeTrue();

        shell.FlowDirection.Should().Be(FlowDirection.LeftToRight);
        shell.Items.Single(item => item.Tab == ShellTab.Students).Label.Should().Be("Students");
        shell.Items.Single(item => item.Tab == ShellTab.Settings).Label.Should().Be("Settings");
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
