using FluentAssertions;
using NSubstitute;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Groups;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Students;
using SchoolManagement.Application.UseCases;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Tests.Shell;

public sealed class CommandPaletteViewModelTests
{
    [Fact]
    public void Opening_lists_the_commands_and_selects_the_first_one()
    {
        var harness = new Harness();
        using var palette = harness.Create();

        palette.Open();

        palette.IsOpen.Should().BeTrue();
        palette.Results.Should().HaveCount(8);
        palette.SelectedResult.Should().BeSameAs(palette.Results[0]);
        palette.Results.Select(result => result.Title).Should().Contain("Open settings");
    }

    [Fact]
    public void Closing_clears_the_results_and_the_query()
    {
        var harness = new Harness();
        using var palette = harness.Create();
        palette.Open();

        palette.Close();

        palette.IsOpen.Should().BeFalse();
        palette.Query.Should().BeNull();
        palette.Results.Should().BeEmpty();
        palette.SelectedResult.Should().BeNull();
    }

    [Fact]
    public async Task A_two_character_query_merges_students_groups_and_commands()
    {
        var harness = new Harness();
        using var palette = harness.Create();
        palette.Open();

        palette.Query = "ali";
        await palette.WhenSearchSettles();

        palette.Results.Select(result => result.Title).Should().Contain("Ali Benali");
        palette.Results.Select(result => result.Title).Should().Contain("3rd A");
        await harness.Students.Received(1)
            .GetListAsync(Arg.Is<StudentListQuery>(query => query.SearchTerm == "ali"), Arg.Any<CancellationToken>());
        await harness.Groups.Received(1)
            .GetListAsync(Arg.Is<GroupListQuery>(query => query.SearchTerm == "ali"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_single_character_never_hits_the_repositories()
    {
        var harness = new Harness();
        using var palette = harness.Create();
        palette.Open();

        palette.Query = "a";
        await palette.WhenSearchSettles();

        await harness.Students.DidNotReceiveWithAnyArgs()
            .GetListAsync(default!, default);
        await harness.Groups.DidNotReceiveWithAnyArgs()
            .GetListAsync(default!, default);
        palette.Results.Should().NotBeEmpty();
    }

    [Fact]
    public void Running_a_command_navigates_and_closes_the_palette()
    {
        var harness = new Harness();
        using var palette = harness.Create();
        palette.Open();
        palette.SelectedResult = palette.Results.Single(result => result.Title == "Open settings");

        palette.RunSelectedCommand.Execute(null);

        palette.IsOpen.Should().BeFalse();
        harness.Navigation.Received(1).NavigateTo(ShellTab.Settings);
    }

    [Fact]
    public void The_theme_and_help_commands_are_events_the_shell_owns()
    {
        var harness = new Harness();
        using var palette = harness.Create();
        palette.Open();

        var themeRaised = false;
        var helpRaised = false;
        palette.ThemeToggleRequested += (_, _) => themeRaised = true;
        palette.ShortcutsRequested += (_, _) => helpRaised = true;

        palette.SelectedResult = palette.Results.Single(result => result.Title == "Toggle theme");
        palette.RunSelectedCommand.Execute(null);

        palette.Open();
        palette.SelectedResult = palette.Results.Single(result => result.Title == "Keyboard shortcuts");
        palette.RunSelectedCommand.Execute(null);

        themeRaised.Should().BeTrue();
        helpRaised.Should().BeTrue();
    }

    [Fact]
    public void Moving_the_selection_wraps_around_the_list()
    {
        var harness = new Harness();
        using var palette = harness.Create();
        palette.Open();

        palette.MoveUpCommand.Execute(null);

        palette.SelectedResult.Should().BeSameAs(palette.Results[^1]);

        palette.MoveDownCommand.Execute(null);

        palette.SelectedResult.Should().BeSameAs(palette.Results[0]);
    }

    private sealed class Harness
    {
        public Harness()
        {
            var localization = ShellViewModelTests.CreateLocalization();
            localization.SetLanguageAsync("en").GetAwaiter().GetResult().Should().BeTrue();
            Localization = localization;

            Students.GetListAsync(Arg.Any<StudentListQuery>(), Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<StudentListItem>>.Success(
                [
                    new StudentListItem(
                        1,
                        "STU-2026-0001",
                        "Ali",
                        "Benali",
                        null,
                        "active",
                        null,
                        null,
                        GroupNames: "3rd A"),
                ]));

            Groups.GetListAsync(Arg.Any<GroupListQuery>(), Arg.Any<CancellationToken>())
                .Returns(Result<IReadOnlyList<GroupSummary>>.Success(
                [
                    new GroupSummary(
                        1,
                        "3rd A",
                        1,
                        "Mathematics",
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        30,
                        1500,
                        true,
                        10),
                ]));
        }

        public INavigationService Navigation { get; } = Substitute.For<INavigationService>();

        public IStudentRepository Students { get; } = Substitute.For<IStudentRepository>();

        public IGroupRepository Groups { get; } = Substitute.For<IGroupRepository>();

        public SchoolManagement.Application.Localization.ILocalizationService Localization { get; }

        public CommandPaletteViewModel Create() => new(
            Navigation,
            Localization,
            new GetStudentsUseCase(Students),
            new GetGroupsUseCase(Groups));
    }
}
