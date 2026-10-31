using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Settings;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Tests.Navigation;

public sealed class NavigationServiceTests
{
    [Theory]
    [InlineData(ShellTab.Dashboard, typeof(DashboardViewModel))]
    [InlineData(ShellTab.Students, typeof(StudentsViewModel))]
    [InlineData(ShellTab.Groups, typeof(GroupsViewModel))]
    [InlineData(ShellTab.Finance, typeof(FinanceViewModel))]
    [InlineData(ShellTab.Settings, typeof(SettingsViewModel))]
    public void NavigateToTab_resolves_the_expected_view_model(ShellTab tab, Type expectedType)
    {
        using var provider = CreateProvider();
        var navigation = provider.GetRequiredService<INavigationService>();

        navigation.NavigateToTab(tab);

        navigation.CurrentTab.Should().Be(tab);
        navigation.CurrentViewModel.Should().BeOfType(expectedType);
    }

    [Fact]
    public void Navigating_back_reuses_the_same_view_model_instance()
    {
        using var provider = CreateProvider();
        var navigation = provider.GetRequiredService<INavigationService>();

        navigation.NavigateToTab(ShellTab.Dashboard);
        var dashboard = navigation.CurrentViewModel;

        navigation.NavigateTo(ShellTab.Students);
        navigation.NavigateToTab(ShellTab.Dashboard);

        navigation.CurrentViewModel.Should().BeSameAs(dashboard);
        navigation.CurrentTab.Should().Be(ShellTab.Dashboard);
    }

    [Fact]
    public void Navigated_event_carries_the_tab_and_the_current_view_model()
    {
        using var provider = CreateProvider();
        var navigation = provider.GetRequiredService<INavigationService>();
        var events = new List<NavigationChangedEventArgs>();
        navigation.Navigated += (_, args) => events.Add(args);

        navigation.NavigateTo(ShellTab.Dashboard);
        navigation.NavigateToTab(ShellTab.Groups);

        events.Should().HaveCount(2);
        events[0].Tab.Should().Be(ShellTab.Dashboard);
        events[1].Tab.Should().Be(ShellTab.Groups);
        events[1].ViewModel.Should().BeSameAs(navigation.CurrentViewModel);
    }

    [Fact]
    public void Opening_a_sub_screen_keeps_the_current_tab_selected()
    {
        using var provider = CreateProvider();
        var navigation = provider.GetRequiredService<INavigationService>();
        navigation.NavigateTo(ShellTab.Students);

        var opened = navigation.NavigateTo<DashboardViewModel>();

        navigation.CurrentTab.Should().Be(ShellTab.Students);
        navigation.CurrentViewModel.Should().BeSameAs(opened);
        opened.Should().BeOfType<DashboardViewModel>();
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton(Substitute.For<ILocalizationService>());
        services.AddSingleton(Substitute.For<IThemeService>());
        services.AddSingleton(Substitute.For<IAppSettingsStore>());

        services.AddSingleton<INavigationService, NavigationService>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<StudentsViewModel>();
        services.AddTransient<GroupsViewModel>();
        services.AddTransient<FinanceViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services.BuildServiceProvider();
    }
}
