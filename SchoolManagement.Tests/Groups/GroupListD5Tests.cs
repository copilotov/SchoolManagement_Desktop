using FluentAssertions;
using NSubstitute;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Groups;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Desktop.Controls;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.ViewModels;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Tests.Groups;

/// <summary>
/// Phase D5 additions to the group screens: the card/table toggle, the level and
/// teacher grouping, the inline form validation, the interactive schedule grid
/// (click-to-add, drag/move through the conflict warning, session generation),
/// the group-file shortcuts and the enrollment capacity warning.
/// </summary>
public sealed class GroupListD5Tests
{
    [Fact]
    public void The_table_and_card_toggle_updates_the_view_flags()
    {
        using var vm = new GroupVmHarness().CreateGroups();

        vm.IsTableView.Should().BeTrue();
        vm.IsCardView.Should().BeFalse();

        vm.ShowCardsCommand.Execute(null);
        vm.IsCardView.Should().BeTrue();
        vm.IsTableView.Should().BeFalse();

        vm.ShowTableCommand.Execute(null);
        vm.IsTableView.Should().BeTrue();
        vm.IsCardView.Should().BeFalse();
    }

    [Fact]
    public async Task Cards_project_capacity_subject_teacher_and_schedule_badges()
    {
        var harness = new GroupVmHarness();
        harness.Groups
            .GetListAsync(Arg.Any<GroupListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<GroupSummary>>.Success(
                new[] { GroupVmHarness.Summary(active: 2, max: 30, slots: 2) }));

        using var vm = harness.CreateGroups();
        await vm.EnsureLoadedAsync();

        vm.HasCards.Should().BeTrue();
        vm.Cards.Should().ContainSingle();
        var card = vm.Cards[0];
        card.Name.Should().Be("Group Alpha");
        card.SubjectText.Should().Be("Mathematics");
        card.TeacherText.Should().Be("Teacher One");
        card.CapacityText.Should().Be("2/30");
        card.CapacityPercent.Should().Be(7);
        card.HasSchedule.Should().BeTrue();
        card.SlotsBadge.Should().Be(TranslationKeys.Groups.SlotsBadge);
        card.SlotsTone.Should().Be(StatusTone.Info);
        card.StatusText.Should().Be(TranslationKeys.Common.Active);
        card.StatusTone.Should().Be(StatusTone.Success);
    }

    [Fact]
    public async Task A_group_without_a_schedule_shows_the_empty_slot_badge()
    {
        var harness = new GroupVmHarness();
        harness.Groups
            .GetListAsync(Arg.Any<GroupListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<GroupSummary>>.Success(
                new[] { GroupVmHarness.Summary(slots: 0) }));

        using var vm = harness.CreateGroups();
        await vm.EnsureLoadedAsync();

        var card = vm.Cards[0];
        card.HasSchedule.Should().BeFalse();
        card.SlotsBadge.Should().Be(TranslationKeys.Groups.NoSlotsShort);
        card.SlotsTone.Should().Be(StatusTone.Neutral);
    }

    [Fact]
    public async Task Grouping_by_level_builds_one_section_per_level()
    {
        var harness = new GroupVmHarness();
        harness.Groups
            .GetListAsync(Arg.Any<GroupListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<GroupSummary>>.Success(new[]
            {
                GroupVmHarness.Summary(1) with { AcademicLevelName = "Level One" },
                GroupVmHarness.Summary(2) with { AcademicLevelName = "Level Two" },
            }));

        using var vm = harness.CreateGroups();
        await vm.EnsureLoadedAsync();

        vm.SelectedGrouping = vm.GroupingOptions.Single(
            option => option.Value == GroupGrouping.Level);

        vm.Sections.Should().HaveCount(2);
        vm.Sections.Should().OnlyContain(section => section.HasTitle);
        vm.Sections.Select(section => section.Title)
            .Should().BeEquivalentTo(new[] { "Level One", "Level Two" });
        vm.Sections.SelectMany(section => section.Items).Should().HaveCount(2);
    }

    [Fact]
    public async Task Grouping_by_teacher_puts_the_teacherless_groups_in_their_own_section()
    {
        var harness = new GroupVmHarness();
        harness.Groups
            .GetListAsync(Arg.Any<GroupListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<GroupSummary>>.Success(new[]
            {
                GroupVmHarness.Summary(1),
                GroupVmHarness.Summary(2) with { TeacherName = null },
            }));

        using var vm = harness.CreateGroups();
        await vm.EnsureLoadedAsync();

        vm.SelectedGrouping = vm.GroupingOptions.Single(
            option => option.Value == GroupGrouping.Teacher);

        vm.Sections.Should().HaveCount(2);
        vm.Sections.Select(section => section.Title)
            .Should().Contain(TranslationKeys.Groups.NoTeacherSection);
    }

    [Fact]
    public async Task No_grouping_yields_one_untitled_section()
    {
        var harness = new GroupVmHarness();
        harness.Groups
            .GetListAsync(Arg.Any<GroupListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<GroupSummary>>.Success(
                new[] { GroupVmHarness.Summary(1), GroupVmHarness.Summary(2) }));

        using var vm = harness.CreateGroups();
        await vm.EnsureLoadedAsync();

        vm.Sections.Should().ContainSingle();
        vm.Sections[0].HasTitle.Should().BeFalse();
        vm.Sections[0].Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task The_editor_validates_name_grade_subject_capacity_and_fee()
    {
        using var vm = new GroupVmHarness().CreateGroups();
        await vm.EnsureLoadedAsync();

        vm.AddCommand.Execute(null);
        vm.ValidateEditor().Should().BeFalse();
        vm.HasNameError.Should().BeTrue();
        vm.HasSubjectError.Should().BeTrue();

        vm.EditorName = "Group Alpha";
        vm.SelectedEditorSubject = vm.Subjects.First();
        vm.EditorMaxStudents = "501";
        vm.EditorMonthlyFee = "1500";

        vm.ValidateEditor().Should().BeFalse();
        vm.HasCapacityError.Should().BeTrue();

        vm.EditorMaxStudents = "500";
        vm.ValidateEditor().Should().BeTrue();
        vm.HasCapacityError.Should().BeFalse();

        vm.EditorMonthlyFee = "-1";
        vm.ValidateEditor().Should().BeFalse();
        vm.HasFeeError.Should().BeTrue();
    }

    [Fact]
    public async Task Clicking_an_empty_cell_opens_the_editor_on_that_hour()
    {
        using var vm = new GroupVmHarness().CreateSchedule();
        await vm.InitializeAsync(1);

        vm.RequestSlot(1, TimeSpan.FromHours(10));

        vm.IsEditorOpen.Should().BeTrue();
        vm.EditingSlotId.Should().BeNull();
        vm.SelectedDay!.Value.Should().Be(1);
        vm.EditorStartTime.Should().Be("10:00");
        vm.EditorEndTime.Should().Be("11:00");
        vm.EditorTitle.Should().Be(TranslationKeys.Groups.AddTimeSlot);
    }

    [Fact]
    public async Task Moving_a_slot_through_a_conflict_asks_before_saving()
    {
        var harness = new GroupVmHarness();
        harness.ScheduleRepository
            .GetSlotsAsync(Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ScheduleSlotItem>>.Success(
                new[] { GroupVmHarness.Slot(day: 0, hour: 8) }));
        harness.ScheduleRepository
            .GetConflictsAsync(
                Arg.Any<int>(),
                Arg.Any<int?>(),
                Arg.Any<int>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<int?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ScheduleSlotItem>>.Success(
                new[] { GroupVmHarness.Slot(day: 1, hour: 10) }));

        using var vm = harness.CreateSchedule();
        await vm.InitializeAsync(1);
        vm.WeekScheduleItems.Should().ContainSingle();

        await vm.MoveSlotAsync(vm.WeekScheduleItems[0], 1, TimeSpan.FromHours(10), TimeSpan.FromHours(11));

        vm.IsConflictOpen.Should().BeTrue();
        vm.Conflicts.Should().ContainSingle();
        await harness.ScheduleRepository.DidNotReceive().SaveSlotAsync(
            Arg.Any<ScheduleSaveRequest>(),
            Arg.Any<CancellationToken>());

        await vm.ConfirmConflictCommand.ExecuteAsync(null);

        await harness.ScheduleRepository.Received(1).SaveSlotAsync(
            Arg.Any<ScheduleSaveRequest>(),
            Arg.Any<CancellationToken>());
        vm.StatusMessage.Should().Be(TranslationKeys.Groups.MoveSaved);
    }

    [Fact]
    public async Task Moving_a_slot_without_a_conflict_saves_directly()
    {
        var harness = new GroupVmHarness();
        harness.ScheduleRepository
            .GetSlotsAsync(Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ScheduleSlotItem>>.Success(
                new[] { GroupVmHarness.Slot(day: 0, hour: 8) }));

        using var vm = harness.CreateSchedule();
        await vm.InitializeAsync(1);

        await vm.MoveSlotAsync(vm.WeekScheduleItems[0], 1, TimeSpan.FromHours(10), TimeSpan.FromHours(11));

        vm.IsConflictOpen.Should().BeFalse();
        await harness.ScheduleRepository.Received(1).SaveSlotAsync(
            Arg.Any<ScheduleSaveRequest>(),
            Arg.Any<CancellationToken>());
        vm.StatusMessage.Should().Be(TranslationKeys.Groups.MoveSaved);
    }

    [Fact]
    public async Task An_invalid_moved_range_is_rejected_before_any_query()
    {
        var harness = new GroupVmHarness();
        harness.ScheduleRepository
            .GetSlotsAsync(Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ScheduleSlotItem>>.Success(
                new[] { GroupVmHarness.Slot(day: 0, hour: 8) }));

        using var vm = harness.CreateSchedule();
        await vm.InitializeAsync(1);

        await vm.MoveSlotAsync(vm.WeekScheduleItems[0], 1, TimeSpan.FromHours(11), TimeSpan.FromHours(10));

        vm.ErrorMessage.Should().Be(TranslationKeys.Groups.InvalidTimeRange);
        await harness.ScheduleRepository.DidNotReceive().SaveSlotAsync(
            Arg.Any<ScheduleSaveRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Generating_sessions_reports_the_created_count()
    {
        var harness = new GroupVmHarness();
        using var vm = harness.CreateSchedule();
        await vm.InitializeAsync(1);

        await vm.GenerateSessionsCommand.ExecuteAsync(null);

        await harness.Sessions.Received(1).GenerateForDateAsync(
            new DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc),
            1,
            Arg.Any<CancellationToken>());
        vm.StatusMessage.Should().Be(TranslationKeys.Groups.GenerationCreated);
    }

    [Fact]
    public async Task The_group_file_shows_the_compact_grid_and_its_shortcuts()
    {
        var harness = new GroupVmHarness();
        harness.ScheduleRepository
            .GetSlotsAsync(Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ScheduleSlotItem>>.Success(
                new[] { GroupVmHarness.Slot(day: 0, hour: 8) }));

        using var vm = harness.CreateDetails();
        await vm.InitializeAsync(1);

        vm.DayLabels.Should().HaveCount(7);
        vm.WeekScheduleItems.Should().ContainSingle();
        vm.WeekScheduleItems[0].DayIndex.Should().Be(0);
        vm.HasSlots.Should().BeTrue();

        vm.OpenAttendanceCommand.Execute(null);
        harness.Navigation.Received(1).NavigateToTab(ShellTab.Attendance);

        vm.OpenHomeworkCommand.Execute(null);
        harness.Navigation.Received(1).NavigateToTab(ShellTab.Homework);
    }

    [Fact]
    public async Task The_enrollment_board_warns_when_the_group_is_full()
    {
        var harness = new GroupVmHarness();
        harness.Groups
            .GetSummaryAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<GroupSummary?>.Success(
                GroupVmHarness.Summary(active: 30, max: 30)));

        using var vm = harness.CreateEnrollment();
        await vm.InitializeAsync(1);

        vm.IsFull.Should().BeTrue();
        vm.ShowCapacityWarning.Should().BeTrue();
        vm.CapacityPercent.Should().Be(100);
        vm.CapacityFullText.Should().Be(TranslationKeys.Enrollment.CapacityFull);
        vm.CapacityWarningText.Should().Be(TranslationKeys.Enrollment.CapacityWarning);
    }
}
