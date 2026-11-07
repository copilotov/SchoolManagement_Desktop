using FluentAssertions;
using NSubstitute;
using SchoolManagement.Application.Attendance;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.ViewModels;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Tests.Attendance;

/// <summary>
/// Behaviour of the take-attendance screen: the roster defaults, the one-click
/// status buttons, the select-all helpers, the read-only rule of a completed
/// session, the guarded bulk save and the QR scan apply step. The scanner rules
/// themselves (duplicate, wrong group, malformed) are proven in
/// <see cref="QrScanSessionTests"/>; here the dialog port is substituted.
/// </summary>
public sealed class TakeAttendanceViewModelTests
{
    [Fact]
    public async Task Loading_the_board_builds_the_header_the_roster_and_the_due_homeworks()
    {
        var harness = new AttendanceVmHarness();
        harness.Homeworks
            .GetDueForSessionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HomeworkListItem>>.Success(
                new[] { AttendanceVmHarness.HomeworkItem() }));

        using var viewModel = harness.CreateTakeAttendance();
        await viewModel.InitializeAsync(4);

        viewModel.SessionId.Should().Be(4);
        viewModel.GroupName.Should().Be("Group Alpha");
        viewModel.DateText.Should().Be("days.wednesday 2026-11-04");
        viewModel.TimeText.Should().Be("09:00 - 10:00");
        viewModel.TypeText.Should().Be("attendance.regular");
        viewModel.StatusText.Should().Be("attendance.scheduled");
        viewModel.TopicText.Should().Be("Algebra");
        viewModel.Rows.Should().HaveCount(2);
        viewModel.HasStudents.Should().BeTrue();
        viewModel.IsEditable.Should().BeTrue();
        viewModel.IsClosed.Should().BeFalse();

        viewModel.PresentCount.Should().Be(2);
        viewModel.AbsentCount.Should().Be(0);
        viewModel.LateCount.Should().Be(0);
        viewModel.TotalCount.Should().Be(2);
        viewModel.PresentCountText.Should().Be("attendance.presentCount");

        viewModel.DueHomeworks.Should().ContainSingle();
        viewModel.DueHomeworks[0].Title.Should().Be("Exercise 1");
        viewModel.DueHomeworks[0].DueText.Should().Be("days.monday 2026-11-09");
        viewModel.DueHomeworks[0].CountsText.Should().Be("1/2");
        viewModel.HasDueHomeworks.Should().BeTrue();
    }

    [Fact]
    public async Task A_blank_roster_status_is_treated_as_present()
    {
        var harness = new AttendanceVmHarness();
        harness.Attendance
            .GetRosterAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<AttendanceRosterItem>>.Success(
            [
                AttendanceVmHarness.RosterRow(1, string.Empty),
                AttendanceVmHarness.RosterRow(2, "   "),
            ]));

        using var viewModel = harness.CreateTakeAttendance();
        await viewModel.InitializeAsync(4);

        viewModel.Rows[0].Status.Should().Be(AttendanceStatuses.Present);
        viewModel.Rows[1].Status.Should().Be(AttendanceStatuses.Present);
        viewModel.PresentCount.Should().Be(2);
    }

    [Fact]
    public async Task The_row_buttons_move_a_student_between_the_three_states()
    {
        var harness = new AttendanceVmHarness();
        using var viewModel = harness.CreateTakeAttendance();
        await viewModel.InitializeAsync(4);

        viewModel.Rows[0].MarkAbsentCommand.Execute(null);
        viewModel.Rows[0].IsAbsent.Should().BeTrue();
        viewModel.Rows[0].IsPresent.Should().BeFalse();
        viewModel.Rows[0].StatusText.Should().Be("attendance.absent");
        viewModel.AbsentCount.Should().Be(1);
        viewModel.PresentCount.Should().Be(1);

        viewModel.Rows[1].MarkLateCommand.Execute(null);
        viewModel.Rows[1].IsLate.Should().BeTrue();
        viewModel.LateCount.Should().Be(1);

        viewModel.Rows[0].MarkPresentCommand.Execute(null);
        viewModel.Rows[0].IsPresent.Should().BeTrue();
        viewModel.PresentCount.Should().Be(1);
        viewModel.LateCount.Should().Be(1);
    }

    [Fact]
    public async Task The_select_all_helpers_apply_to_every_row()
    {
        var harness = new AttendanceVmHarness();
        harness.Attendance
            .GetRosterAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<AttendanceRosterItem>>.Success(
            [
                AttendanceVmHarness.RosterRow(1, AttendanceStatuses.Present),
                AttendanceVmHarness.RosterRow(2, AttendanceStatuses.Present),
                AttendanceVmHarness.RosterRow(3, AttendanceStatuses.Late),
            ]));

        using var viewModel = harness.CreateTakeAttendance();
        await viewModel.InitializeAsync(4);

        viewModel.SetAllAbsentCommand.Execute(null);
        viewModel.AbsentCount.Should().Be(3);
        viewModel.PresentCount.Should().Be(0);
        viewModel.LateCount.Should().Be(0);

        viewModel.SetAllLateCommand.Execute(null);
        viewModel.LateCount.Should().Be(3);
        viewModel.AbsentCount.Should().Be(0);

        viewModel.SetAllPresentCommand.Execute(null);
        viewModel.PresentCount.Should().Be(3);
        viewModel.LateCount.Should().Be(0);
    }

    [Fact]
    public async Task A_completed_session_is_read_only()
    {
        var harness = new AttendanceVmHarness();
        harness.Sessions
            .GetSummaryAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<SessionListItem?>.Success(
                AttendanceVmHarness.SessionItem(status: SessionStatuses.Completed)));

        using var viewModel = harness.CreateTakeAttendance();
        await viewModel.InitializeAsync(4);

        viewModel.IsEditable.Should().BeFalse();
        viewModel.IsClosed.Should().BeTrue();

        viewModel.Rows[0].MarkAbsentCommand.Execute(null);
        viewModel.Rows[0].Status.Should().Be(AttendanceStatuses.Present);
        viewModel.ErrorMessage.Should().Be("attendance.sessionClosed");

        viewModel.SetAllAbsentCommand.Execute(null);
        viewModel.AbsentCount.Should().Be(0);

        viewModel.ErrorMessage = string.Empty;
        await viewModel.SaveCommand.ExecuteAsync(null);
        viewModel.ErrorMessage.Should().Be("attendance.sessionClosed");
        await harness.Attendance.DidNotReceive().UpsertManyAsync(
            Arg.Any<int>(),
            Arg.Any<IReadOnlyList<AttendanceEntry>>(),
            Arg.Any<CancellationToken>());

        viewModel.ErrorMessage = string.Empty;
        await viewModel.ScanQrCommand.ExecuteAsync(null);
        viewModel.ErrorMessage.Should().Be("attendance.sessionClosed");
        await harness.Scanner.DidNotReceive().ScanAsync(Arg.Any<QrScanRequest>());
    }

    [Fact]
    public async Task Saving_writes_every_row_then_completes_the_session()
    {
        var harness = new AttendanceVmHarness();
        using var viewModel = harness.CreateTakeAttendance();
        await viewModel.InitializeAsync(4);

        viewModel.Rows[1].MarkAbsentCommand.Execute(null);
        await viewModel.SaveCommand.ExecuteAsync(null);

        await harness.Attendance.Received(1).UpsertManyAsync(
            4,
            Arg.Is<IReadOnlyList<AttendanceEntry>>(entries =>
                entries.Count == 2
                && entries[0].StudentId == 1
                && entries[0].Status == AttendanceStatuses.Present
                && entries[1].StudentId == 2
                && entries[1].Status == AttendanceStatuses.Absent),
            Arg.Any<CancellationToken>());
        await harness.Sessions.Received(1).SetStatusAsync(
            4,
            SessionStatuses.Completed,
            null,
            Arg.Any<CancellationToken>());
        viewModel.StatusMessage.Should().Be("attendance.saveSuccess");
        viewModel.HasError.Should().BeFalse();
    }

    [Fact]
    public async Task An_empty_roster_does_not_save()
    {
        var harness = new AttendanceVmHarness();
        harness.Attendance
            .GetRosterAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<AttendanceRosterItem>>.Success(
                Array.Empty<AttendanceRosterItem>()));

        using var viewModel = harness.CreateTakeAttendance();
        await viewModel.InitializeAsync(4);

        viewModel.HasStudents.Should().BeFalse();
        await viewModel.SaveCommand.ExecuteAsync(null);

        viewModel.ErrorMessage.Should().Be("attendance.noStudents");
        await harness.Attendance.DidNotReceive().UpsertManyAsync(
            Arg.Any<int>(),
            Arg.Any<IReadOnlyList<AttendanceEntry>>(),
            Arg.Any<CancellationToken>());
        await harness.Scanner.DidNotReceive().ScanAsync(Arg.Any<QrScanRequest>());
    }

    [Fact]
    public async Task A_blocked_license_stops_the_save_and_shows_the_renew_button()
    {
        var harness = new AttendanceVmHarness(AttendanceVmHarness.Blocking());
        using var viewModel = harness.CreateTakeAttendance();
        await viewModel.InitializeAsync(4);

        await viewModel.SaveCommand.ExecuteAsync(null);

        viewModel.ErrorMessage.Should().Be("license.errorWriteBlocked");
        viewModel.IsRenewVisible.Should().BeTrue();
        await harness.Attendance.DidNotReceive().UpsertManyAsync(
            Arg.Any<int>(),
            Arg.Any<IReadOnlyList<AttendanceEntry>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_qr_scan_marks_the_accepted_students_present()
    {
        var harness = new AttendanceVmHarness();
        harness.Scanner
            .ScanAsync(Arg.Any<QrScanRequest>())
            .Returns(new QrScanResult(new[] { 1 }));

        using var viewModel = harness.CreateTakeAttendance();
        await viewModel.InitializeAsync(4);

        viewModel.Rows[0].MarkAbsentCommand.Execute(null);
        viewModel.Rows[1].MarkAbsentCommand.Execute(null);
        viewModel.AbsentCount.Should().Be(2);

        await viewModel.ScanQrCommand.ExecuteAsync(null);

        viewModel.Rows[0].Status.Should().Be(AttendanceStatuses.Present);
        viewModel.Rows[1].Status.Should().Be(AttendanceStatuses.Absent);
        viewModel.PresentCount.Should().Be(1);
        viewModel.StatusMessage.Should().Be("attendance.scanSummary");
        await harness.Scanner.Received(1).ScanAsync(
            Arg.Is<QrScanRequest>(request =>
                request.Students.Count == 2
                && request.Students[0].StudentCode == "STU-2026-0001"
                && request.Title == "attendance.scanQrTitle"));
    }

    [Fact]
    public async Task A_cancelled_scan_changes_nothing()
    {
        var harness = new AttendanceVmHarness();
        harness.Scanner.ScanAsync(Arg.Any<QrScanRequest>()).Returns((QrScanResult?)null);

        using var viewModel = harness.CreateTakeAttendance();
        await viewModel.InitializeAsync(4);

        await viewModel.ScanQrCommand.ExecuteAsync(null);

        viewModel.PresentCount.Should().Be(2);
        viewModel.StatusMessage.Should().BeEmpty();
    }

    [Fact]
    public async Task A_scan_without_an_accepted_student_changes_nothing()
    {
        var harness = new AttendanceVmHarness();
        harness.Scanner
            .ScanAsync(Arg.Any<QrScanRequest>())
            .Returns(new QrScanResult(Array.Empty<int>()));

        using var viewModel = harness.CreateTakeAttendance();
        await viewModel.InitializeAsync(4);

        await viewModel.ScanQrCommand.ExecuteAsync(null);

        viewModel.PresentCount.Should().Be(2);
        viewModel.StatusMessage.Should().BeEmpty();
    }

    [Fact]
    public async Task A_missing_session_is_reported()
    {
        var harness = new AttendanceVmHarness();
        harness.Sessions
            .GetSummaryAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<SessionListItem?>.Success(null));

        using var viewModel = harness.CreateTakeAttendance();
        await viewModel.InitializeAsync(99);

        viewModel.ErrorMessage.Should().Be("attendance.sessionNotFound");
        viewModel.Rows.Should().BeEmpty();
    }

    [Fact]
    public void Back_returns_to_the_attendance_list()
    {
        var harness = new AttendanceVmHarness();
        using var viewModel = harness.CreateTakeAttendance();

        viewModel.BackCommand.Execute(null);

        harness.Navigation.Received(1).NavigateTo<AttendanceViewModel>();
    }

    [Fact]
    public async Task A_language_change_refreshes_every_localized_label()
    {
        var harness = new AttendanceVmHarness();
        harness.Homeworks
            .GetDueForSessionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HomeworkListItem>>.Success(
                new[] { AttendanceVmHarness.HomeworkItem() }));

        using var viewModel = harness.CreateTakeAttendance();
        await viewModel.InitializeAsync(4);

        harness.Localization.Values["attendance.present"] = "Present";
        harness.Localization.Values["attendance.regular"] = "Regular";
        harness.Localization.Values["days.wednesday"] = "Wednesday";
        harness.Localization.Values["days.monday"] = "Monday";
        harness.Localization.RaiseLanguageChanged();

        viewModel.Rows[0].StatusText.Should().Be("Present");
        viewModel.TypeText.Should().Be("Regular");
        viewModel.DateText.Should().Be("Wednesday 2026-11-04");
        viewModel.DueHomeworks[0].DueText.Should().Be("Monday 2026-11-09");
    }
}
