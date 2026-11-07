using FluentAssertions;
using NSubstitute;
using SchoolManagement.Application.Attendance;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Homeworks;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Desktop.ViewModels;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Tests.Homework;

/// <summary>
/// Behaviour of the homework report screen: the default month query, the shared
/// add/edit overlay (group then session cascade), the guarded save, the
/// submissions overlay with its four status buttons and the language rebuild.
/// The validation rules themselves are covered by <see cref="HomeworkUseCaseTests"/>.
/// </summary>
public sealed class HomeworkViewModelTests
{
    private static readonly DateTime Day = Attendance.AttendanceVmHarness.Day;

    [Fact]
    public async Task The_report_asks_for_the_current_month()
    {
        var harness = new Attendance.AttendanceVmHarness();
        using var viewModel = harness.CreateHomework();

        await viewModel.EnsureLoadedAsync();

        await harness.Homeworks.Received(1).GetListAsync(
            Arg.Is<HomeworkReportQuery>(query =>
                query.GroupId == null
                && query.FromDate == new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)
                && query.ToDate == Day),
            Arg.Any<CancellationToken>());
        viewModel.Title.Should().Be("homework.reportTitle");
        viewModel.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task Rows_carry_the_due_date_the_status_and_the_ratio()
    {
        var harness = new Attendance.AttendanceVmHarness();
        harness.Homeworks
            .GetListAsync(Arg.Any<HomeworkReportQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HomeworkListItem>>.Success(
                new[] { Attendance.AttendanceVmHarness.HomeworkItem() }));

        using var viewModel = harness.CreateHomework();
        await viewModel.EnsureLoadedAsync();

        viewModel.Items.Should().ContainSingle();
        var row = viewModel.Items[0];
        row.Id.Should().Be(3);
        row.Title.Should().Be("Exercise 1");
        row.Description.Should().Be("Page 10");
        row.DueText.Should().Be("days.monday 2026-11-09");
        row.CountsText.Should().Be("1 / 2");
        row.SubmissionCount.Should().Be(2);
        row.SubmittedCount.Should().Be(1);
        row.PendingCount.Should().Be(1);
        row.StatusText.Should().Be("homework.pending");
        row.TargetSessionText.Should().BeNull();
        viewModel.SelectedItem.Should().BeSameAs(row);
    }

    [Fact]
    public async Task A_read_failure_is_shown_without_the_renew_button()
    {
        var harness = new Attendance.AttendanceVmHarness();
        harness.Homeworks
            .GetListAsync(Arg.Any<HomeworkReportQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HomeworkListItem>>.Fail(
                Failure.Database("errors.databaseFailure", "boom")));

        using var viewModel = harness.CreateHomework();
        await viewModel.EnsureLoadedAsync();

        viewModel.State.Should().BeOfType<UiState<IReadOnlyList<HomeworkRow>>.Error>();
        viewModel.ErrorMessage.Should().Be("errors.databaseFailure");
        viewModel.IsRenewVisible.Should().BeFalse();
    }

    [Fact]
    public async Task The_group_filter_is_sent_with_the_next_query()
    {
        var harness = new Attendance.AttendanceVmHarness();
        using var viewModel = harness.CreateHomework();
        await viewModel.EnsureLoadedAsync();

        viewModel.Groups.Should().HaveCount(2);
        viewModel.Groups[0].Label.Should().Be("homework.allGroups");
        viewModel.SelectedGroup = viewModel.Groups[1];
        viewModel.GroupFilterText.Should().Be("Group Alpha");
        viewModel.CreateQuery().GroupId.Should().Be(1);

        await viewModel.SearchCommand.ExecuteAsync(null);

        await harness.Homeworks.Received().GetListAsync(
            Arg.Is<HomeworkReportQuery>(query => query.GroupId == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Add_opens_the_editor_on_the_filtered_group_and_its_sessions()
    {
        var harness = new Attendance.AttendanceVmHarness();
        harness.Sessions
            .GetListAsync(Arg.Any<SessionListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<SessionListItem>>.Success(
                new[] { Attendance.AttendanceVmHarness.SessionItem() }));

        using var viewModel = harness.CreateHomework();
        await viewModel.EnsureLoadedAsync();

        await viewModel.AddCommand.ExecuteAsync(null);

        viewModel.IsEditorOpen.Should().BeTrue();
        viewModel.EditorTitle.Should().Be("homework.add");
        viewModel.EditingId.Should().BeNull();
        viewModel.EditorDueDate.Should().Be("2026-11-04");
        viewModel.SelectedEditorGroup!.Id.Should().Be(1);
        viewModel.EditorSessions.Should().ContainSingle().Which.Id.Should().Be(4);
        viewModel.HasEditorSessions.Should().BeTrue();
        viewModel.HasNoEditorSessions.Should().BeFalse();
        viewModel.TargetSessions.Should().HaveCount(2);
        viewModel.TargetSessions[0].Id.Should().Be(0);
        viewModel.TargetSessions[0].Label.Should().Be("homework.noSession");
        viewModel.EditorSessionsHint.Should().BeEmpty();
        await harness.Sessions.Received().GetListAsync(
            Arg.Is<SessionListQuery>(query => query.GroupId == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_group_without_a_session_shows_the_hint()
    {
        var harness = new Attendance.AttendanceVmHarness();
        using var viewModel = harness.CreateHomework();
        await viewModel.EnsureLoadedAsync();

        await viewModel.AddCommand.ExecuteAsync(null);

        viewModel.EditorSessions.Should().BeEmpty();
        viewModel.HasEditorSessions.Should().BeFalse();
        viewModel.HasNoEditorSessions.Should().BeTrue();
        viewModel.EditorSessionsHint.Should().Be("homework.noSessionsAvailable");
    }

    [Fact]
    public async Task Saving_the_editor_trims_the_request_and_reports_the_creation()
    {
        var harness = new Attendance.AttendanceVmHarness();
        harness.Sessions
            .GetListAsync(Arg.Any<SessionListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<SessionListItem>>.Success(
                new[] { Attendance.AttendanceVmHarness.SessionItem() }));

        using var viewModel = harness.CreateHomework();
        await viewModel.EnsureLoadedAsync();
        await viewModel.AddCommand.ExecuteAsync(null);

        viewModel.EditorTitleText = "  Exercise 2  ";
        viewModel.EditorDescription = "  Page 20  ";
        viewModel.EditorNotes = "  Bring the copybook  ";
        viewModel.EditorDueDate = "2026-11-10";
        await viewModel.SaveEditorCommand.ExecuteAsync(null);

        await harness.Homeworks.Received(1).AddAsync(
            Arg.Is<SchoolManagement.Domain.Entities.Homework>(homework =>
                homework.GroupId == 1
                && homework.SessionId == 4
                && homework.TargetSessionId == null
                && homework.Title == "Exercise 2"
                && homework.Description == "Page 20"
                && homework.Notes == "Bring the copybook"
                && homework.DueDate == new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc)),
            Arg.Any<CancellationToken>());
        viewModel.IsEditorOpen.Should().BeFalse();
        viewModel.StatusMessage.Should().Be("homework.addedSuccess");
        viewModel.IsRenewVisible.Should().BeFalse();
    }

    [Fact]
    public async Task The_target_session_zero_means_no_target()
    {
        var harness = new Attendance.AttendanceVmHarness();
        harness.Sessions
            .GetListAsync(Arg.Any<SessionListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<SessionListItem>>.Success(
                new[] { Attendance.AttendanceVmHarness.SessionItem() }));

        using var viewModel = harness.CreateHomework();
        await viewModel.EnsureLoadedAsync();
        await viewModel.AddCommand.ExecuteAsync(null);

        viewModel.EditorTitleText = "Exercise 2";
        viewModel.EditorDescription = "Page 20";
        viewModel.SelectedTargetSession = viewModel.TargetSessions[0];
        await viewModel.SaveEditorCommand.ExecuteAsync(null);

        await harness.Homeworks.Received(1).AddAsync(
            Arg.Is<SchoolManagement.Domain.Entities.Homework>(homework => homework.TargetSessionId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_blank_title_is_refused_before_the_repository()
    {
        var harness = new Attendance.AttendanceVmHarness();
        harness.Sessions
            .GetListAsync(Arg.Any<SessionListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<SessionListItem>>.Success(
                new[] { Attendance.AttendanceVmHarness.SessionItem() }));

        using var viewModel = harness.CreateHomework();
        await viewModel.EnsureLoadedAsync();
        await viewModel.AddCommand.ExecuteAsync(null);

        viewModel.EditorTitleText = "   ";
        viewModel.EditorDescription = "Page 20";
        await viewModel.SaveEditorCommand.ExecuteAsync(null);

        viewModel.ErrorMessage.Should().Be("validators.required");
        viewModel.IsEditorOpen.Should().BeTrue();
        await harness.Homeworks.DidNotReceive().AddAsync(
            Arg.Any<SchoolManagement.Domain.Entities.Homework>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_missing_session_is_refused_before_the_repository()
    {
        var harness = new Attendance.AttendanceVmHarness();
        harness.Sessions
            .GetListAsync(Arg.Any<SessionListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<SessionListItem>>.Success(
                new[] { Attendance.AttendanceVmHarness.SessionItem() }));

        using var viewModel = harness.CreateHomework();
        await viewModel.EnsureLoadedAsync();
        await viewModel.AddCommand.ExecuteAsync(null);

        viewModel.EditorTitleText = "Exercise 2";
        viewModel.EditorDescription = "Page 20";
        viewModel.SelectedEditorSession = null;
        await viewModel.SaveEditorCommand.ExecuteAsync(null);

        viewModel.ErrorMessage.Should().Be("homework.noSessionsAvailable");
        await harness.Homeworks.DidNotReceive().AddAsync(
            Arg.Any<SchoolManagement.Domain.Entities.Homework>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_blocked_license_keeps_the_editor_open()
    {
        var harness = new Attendance.AttendanceVmHarness(Attendance.AttendanceVmHarness.Blocking());
        harness.Sessions
            .GetListAsync(Arg.Any<SessionListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<SessionListItem>>.Success(
                new[] { Attendance.AttendanceVmHarness.SessionItem() }));

        using var viewModel = harness.CreateHomework();
        await viewModel.EnsureLoadedAsync();
        await viewModel.AddCommand.ExecuteAsync(null);

        viewModel.EditorTitleText = "Exercise 2";
        viewModel.EditorDescription = "Page 20";
        await viewModel.SaveEditorCommand.ExecuteAsync(null);

        viewModel.ErrorMessage.Should().Be("license.errorWriteBlocked");
        viewModel.IsRenewVisible.Should().BeTrue();
        viewModel.IsEditorOpen.Should().BeTrue();
        await harness.Homeworks.DidNotReceive().AddAsync(
            Arg.Any<SchoolManagement.Domain.Entities.Homework>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Edit_loads_the_row_then_updates_the_same_homework()
    {
        var harness = new Attendance.AttendanceVmHarness();
        harness.Homeworks
            .GetListAsync(Arg.Any<HomeworkReportQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HomeworkListItem>>.Success(
                new[] { Attendance.AttendanceVmHarness.HomeworkItem() }));
        harness.Sessions
            .GetListAsync(Arg.Any<SessionListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<SessionListItem>>.Success(
                new[] { Attendance.AttendanceVmHarness.SessionItem() }));

        using var viewModel = harness.CreateHomework();
        await viewModel.EnsureLoadedAsync();

        await viewModel.EditCommand.ExecuteAsync(viewModel.Items[0]);

        viewModel.IsEditorOpen.Should().BeTrue();
        viewModel.EditorTitle.Should().Be("homework.edit");
        viewModel.EditingId.Should().Be(3);
        viewModel.EditorTitleText.Should().Be("Exercise 1");
        viewModel.EditorDescription.Should().Be("Page 10");
        viewModel.EditorDueDate.Should().Be("2026-11-09");
        viewModel.SelectedEditorSession!.Id.Should().Be(4);
        viewModel.SelectedTargetSession!.Id.Should().Be(4);

        viewModel.EditorTitleText = "Exercise 1 revised";
        await viewModel.SaveEditorCommand.ExecuteAsync(null);

        await harness.Homeworks.Received(1).UpdateAsync(
            Arg.Is<SchoolManagement.Domain.Entities.Homework>(homework =>
                homework.Id == 3 && homework.Title == "Exercise 1 revised"),
            Arg.Any<CancellationToken>());
        viewModel.StatusMessage.Should().Be("homework.updatedSuccess");
    }

    [Fact]
    public async Task Editing_a_missing_homework_reports_not_found()
    {
        var harness = new Attendance.AttendanceVmHarness();
        harness.Homeworks
            .GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<SchoolManagement.Domain.Entities.Homework?>.Success(null));
        harness.Sessions
            .GetListAsync(Arg.Any<SessionListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<SessionListItem>>.Success(
                new[] { Attendance.AttendanceVmHarness.SessionItem() }));

        using var viewModel = harness.CreateHomework();
        await viewModel.EnsureLoadedAsync();
        await viewModel.AddCommand.ExecuteAsync(null);
        viewModel.EditingId = 55;
        viewModel.EditorTitleText = "Exercise";
        viewModel.EditorDescription = "Body";

        await viewModel.SaveEditorCommand.ExecuteAsync(null);

        viewModel.ErrorMessage.Should().Be("homework.notFound");
        await harness.Homeworks.DidNotReceive().UpdateAsync(
            Arg.Any<SchoolManagement.Domain.Entities.Homework>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancel_editor_closes_the_overlay_without_writing()
    {
        var harness = new Attendance.AttendanceVmHarness();
        using var viewModel = harness.CreateHomework();
        await viewModel.EnsureLoadedAsync();
        await viewModel.AddCommand.ExecuteAsync(null);

        viewModel.EditorTitleText = "Draft";
        viewModel.CancelEditorCommand.Execute(null);

        viewModel.IsEditorOpen.Should().BeFalse();
        viewModel.EditorTitleText.Should().BeEmpty();
        await harness.Homeworks.DidNotReceive().AddAsync(
            Arg.Any<SchoolManagement.Domain.Entities.Homework>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_submissions_overlay_lists_the_students_and_the_ratio()
    {
        var harness = new Attendance.AttendanceVmHarness();
        harness.Homeworks
            .GetListAsync(Arg.Any<HomeworkReportQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HomeworkListItem>>.Success(
                new[] { Attendance.AttendanceVmHarness.HomeworkItem() }));

        using var viewModel = harness.CreateHomework();
        await viewModel.EnsureLoadedAsync();

        await viewModel.OpenSubmissionsCommand.ExecuteAsync(viewModel.Items[0]);

        viewModel.IsSubmissionsOpen.Should().BeTrue();
        viewModel.SubmissionsTitle.Should().Be("Exercise 1");
        viewModel.SubmissionRows.Should().HaveCount(2);
        viewModel.SubmissionsSummary.Should().Be("1 / 2");
        viewModel.HasSubmissions.Should().BeTrue();
        viewModel.HasNoSubmissions.Should().BeFalse();
        viewModel.SubmissionRows[0].StatusText.Should().Be("homework.submitted");
        viewModel.SubmissionRows[0].IsSubmitted.Should().BeTrue();
        viewModel.SubmissionRows[1].StatusText.Should().Be("homework.pending");
        viewModel.SubmissionRows[1].IsPending.Should().BeTrue();
    }

    [Fact]
    public async Task A_submission_button_writes_the_new_status_and_reloads()
    {
        var harness = new Attendance.AttendanceVmHarness();
        // The due date is already past, so the pure policy stores a real "late"
        // instead of the "submitted" an early click would produce.
        var overdue = new HomeworkListItem(
            3, 1, 4, 4, "Exercise 1", "Page 10", Day.AddDays(-2), false, 2, 1, 1);
        harness.Homeworks
            .GetListAsync(Arg.Any<HomeworkReportQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HomeworkListItem>>.Success(new[] { overdue }));
        harness.Homeworks
            .GetSummaryAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<HomeworkListItem?>.Success(overdue));

        using var viewModel = harness.CreateHomework();
        await viewModel.EnsureLoadedAsync();
        await viewModel.OpenSubmissionsCommand.ExecuteAsync(viewModel.Items[0]);

        await viewModel.SubmissionRows[1].MarkLateCommand.ExecuteAsync(null);

        await harness.Homeworks.Received(1).SetSubmissionStatusAsync(
            3,
            2,
            HomeworkStatuses.Late,
            Arg.Any<DateTime?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
        viewModel.StatusMessage.Should().Be("homework.submissionSaved");
        // The board is read again after a status write, so the overlay and the
        // report ratio cannot drift from the stored rows.
        await harness.Homeworks.Received(2).GetSubmissionBoardAsync(3, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_blocked_license_keeps_the_submission_status_unchanged()
    {
        var harness = new Attendance.AttendanceVmHarness(Attendance.AttendanceVmHarness.Blocking());
        harness.Homeworks
            .GetListAsync(Arg.Any<HomeworkReportQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HomeworkListItem>>.Success(
                new[] { Attendance.AttendanceVmHarness.HomeworkItem() }));

        using var viewModel = harness.CreateHomework();
        await viewModel.EnsureLoadedAsync();
        await viewModel.OpenSubmissionsCommand.ExecuteAsync(viewModel.Items[0]);

        await viewModel.SubmissionRows[1].MarkMissingCommand.ExecuteAsync(null);

        viewModel.ErrorMessage.Should().Be("license.errorWriteBlocked");
        viewModel.IsRenewVisible.Should().BeTrue();
        await harness.Homeworks.DidNotReceive().SetSubmissionStatusAsync(
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<string>(),
            Arg.Any<DateTime?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Closing_the_submissions_overlay_clears_it()
    {
        var harness = new Attendance.AttendanceVmHarness();
        harness.Homeworks
            .GetListAsync(Arg.Any<HomeworkReportQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HomeworkListItem>>.Success(
                new[] { Attendance.AttendanceVmHarness.HomeworkItem() }));

        using var viewModel = harness.CreateHomework();
        await viewModel.EnsureLoadedAsync();
        await viewModel.OpenSubmissionsCommand.ExecuteAsync(viewModel.Items[0]);

        viewModel.CloseSubmissionsCommand.Execute(null);

        viewModel.IsSubmissionsOpen.Should().BeFalse();
        viewModel.SubmissionRows.Should().BeEmpty();
        viewModel.HasNoSubmissions.Should().BeTrue();
    }

    [Fact]
    public async Task A_language_change_rebuilds_the_rows_and_the_editor_title()
    {
        var harness = new Attendance.AttendanceVmHarness();
        harness.Homeworks
            .GetListAsync(Arg.Any<HomeworkReportQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HomeworkListItem>>.Success(
                new[] { Attendance.AttendanceVmHarness.HomeworkItem() }));

        using var viewModel = harness.CreateHomework();
        await viewModel.EnsureLoadedAsync();
        await viewModel.AddCommand.ExecuteAsync(null);

        harness.Localization.Values["homework.allGroups"] = "All groups";
        harness.Localization.Values["homework.pending"] = "Pending";
        harness.Localization.Values["homework.add"] = "Add homework";
        harness.Localization.RaiseLanguageChanged();

        viewModel.Groups[0].Label.Should().Be("All groups");
        viewModel.Items[0].StatusText.Should().Be("Pending");
        viewModel.EditorTitle.Should().Be("Add homework");
    }

    [Fact]
    public void Back_returns_to_the_attendance_list()
    {
        var harness = new Attendance.AttendanceVmHarness();
        using var viewModel = harness.CreateHomework();

        viewModel.BackCommand.Execute(null);

        harness.Navigation.Received(1).NavigateTo<AttendanceViewModel>();
    }
}
