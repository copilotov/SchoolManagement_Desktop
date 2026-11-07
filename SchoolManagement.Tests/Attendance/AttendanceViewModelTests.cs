using FluentAssertions;
using NSubstitute;
using SchoolManagement.Application.Attendance;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Groups;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.UseCases;
using SchoolManagement.Desktop.ViewModels;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Tests.Attendance;

/// <summary>
/// Behaviour of the attendance list ViewModel: the two tabs, session generation,
/// the extra/makeup session form, the guarded cancel/postpone/restore flow with
/// the optional SMS suggestion, the extra-session payment and the navigation to
/// the take-attendance screen. The real use cases run on substituted
/// repositories, so the license guard and the validator are exercised too.
/// </summary>
public sealed class AttendanceViewModelTests
{
    private static readonly DateTime Day = AttendanceVmHarness.Day;

    [Fact]
    public async Task The_first_tab_asks_only_for_todays_sessions()
    {
        var harness = new AttendanceVmHarness();
        harness.Sessions
            .GetListAsync(Arg.Any<SessionListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<SessionListItem>>.Success(
                new[] { AttendanceVmHarness.SessionItem() }));

        using var viewModel = harness.CreateAttendance();
        await viewModel.EnsureLoadedAsync();

        await harness.Sessions.Received(1).GetListAsync(
            Arg.Is<SessionListQuery>(query => query.FromDate == Day && query.ToDate == Day),
            Arg.Any<CancellationToken>());

        viewModel.Items.Should().ContainSingle();
        viewModel.Items[0].Id.Should().Be(4);
        viewModel.Items[0].GroupName.Should().Be("Group Alpha");
        viewModel.Items[0].TimeText.Should().Be("09:00 - 10:00");
        viewModel.Items[0].DateText.Should().Be("days.wednesday 2026-11-04");
        viewModel.Items[0].TopicText.Should().Be("Algebra");
        viewModel.Items[0].StatusText.Should().Be("attendance.scheduled");
        viewModel.Items[0].TypeText.Should().Be("attendance.regular");
        viewModel.IsEmpty.Should().BeFalse();
        viewModel.SelectedItem.Should().BeSameAs(viewModel.Items[0]);
    }

    [Fact]
    public async Task The_second_tab_asks_for_every_session()
    {
        var harness = new AttendanceVmHarness();
        using var viewModel = harness.CreateAttendance();
        viewModel.SelectedTabIndex = 1;

        await viewModel.EnsureLoadedAsync();

        viewModel.IsTodayTab.Should().BeFalse();
        await harness.Sessions.Received(1).GetListAsync(
            Arg.Is<SessionListQuery>(query => query.FromDate == null && query.ToDate == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_group_lookups_put_all_groups_first_and_refresh_the_filter()
    {
        var harness = new AttendanceVmHarness();
        using var viewModel = harness.CreateAttendance();
        await viewModel.EnsureLoadedAsync();

        viewModel.Groups.Should().HaveCount(2);
        viewModel.Groups[0].Id.Should().BeNull();
        viewModel.Groups[0].Label.Should().Be("students.allGroups");
        viewModel.Groups[1].Should().Be(new SessionGroupOption(1, "Group Alpha"));
        viewModel.ExtraGroups.Should().ContainSingle().Which.Id.Should().Be(1);
        viewModel.SelectedGroup.Should().Be(viewModel.Groups[0]);

        viewModel.SelectedGroup = viewModel.Groups[1];
        viewModel.CreateQuery().GroupId.Should().Be(1);

        await viewModel.SearchCommand.ExecuteAsync(null);

        await harness.Sessions.Received().GetListAsync(
            Arg.Is<SessionListQuery>(query => query.GroupId == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_empty_list_shows_the_tab_specific_message()
    {
        var harness = new AttendanceVmHarness();
        using var viewModel = harness.CreateAttendance();

        await viewModel.EnsureLoadedAsync();

        viewModel.IsEmpty.Should().BeTrue();
        viewModel.EmptyMessage.Should().Be("attendance.noSessionsToday");

        viewModel.SelectedTabIndex = 1;
        viewModel.EmptyMessage.Should().Be("attendance.noSessions");
    }

    [Fact]
    public async Task A_read_failure_shows_the_error_without_the_renew_button()
    {
        var harness = new AttendanceVmHarness();
        harness.Sessions
            .GetListAsync(Arg.Any<SessionListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<SessionListItem>>.Fail(
                Failure.Database("errors.databaseFailure", "boom")));

        using var viewModel = harness.CreateAttendance();
        await viewModel.EnsureLoadedAsync();

        viewModel.State.Should().BeOfType<UiState<IReadOnlyList<SessionRow>>.Error>();
        viewModel.ErrorMessage.Should().Be("errors.databaseFailure");
        viewModel.IsRenewVisible.Should().BeFalse();
        viewModel.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Generation_reports_the_created_count()
    {
        var harness = new AttendanceVmHarness();
        harness.Sessions
            .GenerateForDateAsync(Arg.Any<DateTime>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Result<SessionGenerationResult>.Success(new SessionGenerationResult(3, 0)));

        using var viewModel = harness.CreateAttendance();
        await viewModel.EnsureLoadedAsync();

        await viewModel.GenerateSessionsCommand.ExecuteAsync(null);

        viewModel.StatusMessage.Should().Be("groups.generationCreated");
        viewModel.HasError.Should().BeFalse();
        await harness.Sessions.Received(1).GenerateForDateAsync(
            Day,
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Generation_distinguishes_already_existing_from_no_template()
    {
        var harness = new AttendanceVmHarness();
        using var viewModel = harness.CreateAttendance();
        await viewModel.EnsureLoadedAsync();

        harness.Sessions
            .GenerateForDateAsync(Arg.Any<DateTime>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Result<SessionGenerationResult>.Success(new SessionGenerationResult(0, 2)));
        await viewModel.GenerateSessionsCommand.ExecuteAsync(null);
        viewModel.StatusMessage.Should().Be("groups.generationAlreadyExists");

        harness.Sessions
            .GenerateForDateAsync(Arg.Any<DateTime>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Result<SessionGenerationResult>.Success(new SessionGenerationResult(0, 0)));
        await viewModel.GenerateSessionsCommand.ExecuteAsync(null);
        viewModel.StatusMessage.Should().Be("groups.generationNone");
    }

    [Fact]
    public async Task Generation_refuses_a_malformed_date_without_calling_the_repository()
    {
        var harness = new AttendanceVmHarness();
        using var viewModel = harness.CreateAttendance();
        await viewModel.EnsureLoadedAsync();

        viewModel.SelectedDate = "04-11-2026";
        await viewModel.GenerateSessionsCommand.ExecuteAsync(null);

        viewModel.ErrorMessage.Should().Be("validators.invalidDate");
        await harness.Sessions.DidNotReceive().GenerateForDateAsync(
            Arg.Any<DateTime>(),
            Arg.Any<int?>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("2026-11-04", 2026, 11, 4)]
    [InlineData("2026/11/04", 2026, 11, 4)]
    [InlineData("04/11/2026", 2026, 11, 4)]
    [InlineData(" 2026-11-04 ", 2026, 11, 4)]
    public void The_selected_date_accepts_the_three_supported_formats(string value, int year, int month, int day)
    {
        var harness = new AttendanceVmHarness();
        using var viewModel = harness.CreateAttendance();

        viewModel.SelectedDate = value;

        viewModel.ParseSelectedDate().Should().Be(new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void A_malformed_selected_date_is_reported_as_null()
    {
        var harness = new AttendanceVmHarness();
        using var viewModel = harness.CreateAttendance();

        viewModel.SelectedDate = "tomorrow";

        viewModel.ParseSelectedDate().Should().BeNull();
    }

    [Fact]
    public async Task An_extra_session_is_created_from_the_trimmed_form()
    {
        var harness = new AttendanceVmHarness();
        using var viewModel = harness.CreateAttendance();
        await viewModel.EnsureLoadedAsync();

        viewModel.OpenExtraSessionCommand.Execute(null);
        viewModel.IsExtraSessionOpen.Should().BeTrue();
        viewModel.SelectedExtraGroup!.Id.Should().Be(1);
        viewModel.ExtraDate.Should().Be("2026-11-04");

        viewModel.SelectedExtraType = viewModel.SessionTypes.Single(option => option.Value == SessionTypes.Makeup);
        viewModel.ExtraStartTime = "10:00";
        viewModel.ExtraEndTime = "11:30";
        viewModel.ExtraPrice = "1500";
        viewModel.ExtraTopic = "  Revision  ";
        viewModel.ExtraNotes = "  Bring the book  ";
        await viewModel.SaveExtraSessionCommand.ExecuteAsync(null);

        await harness.Sessions.Received(1).CreateExtraAsync(
            Arg.Is<ExtraSessionRequest>(request =>
                request.GroupId == 1
                && request.SessionDate == Day
                && request.StartTime == new TimeSpan(10, 0, 0)
                && request.EndTime == new TimeSpan(11, 30, 0)
                && request.Type == SessionTypes.Makeup
                && request.LessonTopic == "Revision"
                && request.TeacherNotes == "Bring the book"
                && request.Price == 1500),
            Arg.Any<CancellationToken>());
        viewModel.IsExtraSessionOpen.Should().BeFalse();
        viewModel.StatusMessage.Should().Be("attendance.sessionCreated");
    }

    [Fact]
    public async Task An_existing_extra_session_is_reported_instead_of_created()
    {
        var harness = new AttendanceVmHarness();
        harness.Sessions
            .CreateExtraAsync(Arg.Any<ExtraSessionRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<ExtraSessionOutcome>.Success(new ExtraSessionOutcome(12, false)));

        using var viewModel = harness.CreateAttendance();
        await viewModel.EnsureLoadedAsync();

        viewModel.OpenExtraSessionCommand.Execute(null);
        await viewModel.SaveExtraSessionCommand.ExecuteAsync(null);

        viewModel.StatusMessage.Should().Be("attendance.sessionExists");
    }

    [Fact]
    public async Task An_inverted_time_range_is_refused_before_the_repository()
    {
        var harness = new AttendanceVmHarness();
        using var viewModel = harness.CreateAttendance();
        await viewModel.EnsureLoadedAsync();

        viewModel.OpenExtraSessionCommand.Execute(null);
        viewModel.ExtraStartTime = "11:00";
        viewModel.ExtraEndTime = "10:00";
        await viewModel.SaveExtraSessionCommand.ExecuteAsync(null);

        viewModel.ErrorMessage.Should().Be("attendance.invalidTimeRange");
        await harness.Sessions.DidNotReceive().CreateExtraAsync(
            Arg.Any<ExtraSessionRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_extra_session_form_requires_a_group()
    {
        var harness = new AttendanceVmHarness();
        using var viewModel = harness.CreateAttendance();
        await viewModel.EnsureLoadedAsync();

        viewModel.OpenExtraSessionCommand.Execute(null);
        viewModel.SelectedExtraGroup = null;
        await viewModel.SaveExtraSessionCommand.ExecuteAsync(null);

        viewModel.ErrorMessage.Should().Be("validators.required");
        await harness.Sessions.DidNotReceive().CreateExtraAsync(
            Arg.Any<ExtraSessionRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancelling_a_session_writes_the_reason_and_offers_the_sms()
    {
        var harness = new AttendanceVmHarness();
        harness.Sessions
            .GetListAsync(Arg.Any<SessionListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<SessionListItem>>.Success(
                new[] { AttendanceVmHarness.SessionItem() }));

        using var viewModel = harness.CreateAttendance();
        await viewModel.EnsureLoadedAsync();

        viewModel.CancelSessionCommand.Execute(viewModel.Items[0]);
        viewModel.IsStatusOverlayOpen.Should().BeTrue();
        viewModel.StatusOverlayTitle.Should().Be("attendance.cancelSession");

        viewModel.StatusReason = "  Teacher sick  ";
        await viewModel.ConfirmStatusCommand.ExecuteAsync(null);

        await harness.Sessions.Received(1).SetStatusAsync(
            4,
            SessionStatuses.Cancelled,
            "Teacher sick",
            Arg.Any<CancellationToken>());
        await harness.Dialogs.Received(1).ConfirmAsync(
            Arg.Any<string>(),
            "attendance.smsSuggestion",
            "common.yes",
            "common.no");
        await harness.SmsLauncher.Received(1).OpenAsync(
            Arg.Is<IReadOnlyList<string>>(phones => phones.SequenceEqual(new[] { "+213555000001" })),
            Arg.Any<string>());
        viewModel.StatusMessage.Should().Be("attendance.smsSent");
        viewModel.IsStatusOverlayOpen.Should().BeFalse();
    }

    [Fact]
    public async Task Declining_the_sms_keeps_the_status_change_only()
    {
        var harness = new AttendanceVmHarness();
        harness.Sessions
            .GetListAsync(Arg.Any<SessionListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<SessionListItem>>.Success(
                new[] { AttendanceVmHarness.SessionItem() }));
        harness.Dialogs
            .ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(false);

        using var viewModel = harness.CreateAttendance();
        await viewModel.EnsureLoadedAsync();

        viewModel.CancelSessionCommand.Execute(viewModel.Items[0]);
        await viewModel.ConfirmStatusCommand.ExecuteAsync(null);

        viewModel.StatusMessage.Should().Be("attendance.sessionCancelled");
        await harness.Sms.DidNotReceive().SendAsync(
            Arg.Any<SmsSendRequest>(),
            Arg.Any<CancellationToken>());
        await harness.SmsLauncher.DidNotReceive().OpenAsync(
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<string>());
    }

    [Fact]
    public async Task A_messaging_application_that_refuses_to_open_is_reported()
    {
        var harness = new AttendanceVmHarness();
        harness.Sessions
            .GetListAsync(Arg.Any<SessionListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<SessionListItem>>.Success(
                new[] { AttendanceVmHarness.SessionItem() }));
        harness.SmsLauncher
            .OpenAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<string>())
            .Returns(false);

        using var viewModel = harness.CreateAttendance();
        await viewModel.EnsureLoadedAsync();

        viewModel.CancelSessionCommand.Execute(viewModel.Items[0]);
        await viewModel.ConfirmStatusCommand.ExecuteAsync(null);

        viewModel.ErrorMessage.Should().Be("attendance.smsOpenFailed");
        viewModel.StatusMessage.Should().BeEmpty();
    }

    [Fact]
    public async Task The_status_overlay_can_be_cancelled_without_writing()
    {
        var harness = new AttendanceVmHarness();
        harness.Sessions
            .GetListAsync(Arg.Any<SessionListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<SessionListItem>>.Success(
                new[] { AttendanceVmHarness.SessionItem() }));

        using var viewModel = harness.CreateAttendance();
        await viewModel.EnsureLoadedAsync();

        viewModel.PostponeSessionCommand.Execute(viewModel.Items[0]);
        viewModel.StatusReason = "ignored";
        viewModel.CancelStatusOverlayCommand.Execute(null);

        viewModel.IsStatusOverlayOpen.Should().BeFalse();
        viewModel.StatusReason.Should().BeEmpty();
        await harness.Sessions.DidNotReceive().SetStatusAsync(
            Arg.Any<int>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Restoring_a_postponed_session_does_not_suggest_the_sms()
    {
        var harness = new AttendanceVmHarness();
        harness.Sessions
            .GetListAsync(Arg.Any<SessionListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<SessionListItem>>.Success(
                new[] { AttendanceVmHarness.SessionItem(status: SessionStatuses.Postponed) }));
        harness.Sessions
            .GetSummaryAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<SessionListItem?>.Success(
                AttendanceVmHarness.SessionItem(status: SessionStatuses.Postponed)));

        using var viewModel = harness.CreateAttendance();
        await viewModel.EnsureLoadedAsync();

        await viewModel.RestoreSessionCommand.ExecuteAsync(viewModel.Items[0]);

        await harness.Sessions.Received(1).SetStatusAsync(
            4,
            SessionStatuses.Scheduled,
            null,
            Arg.Any<CancellationToken>());
        _ = harness.Dialogs.DidNotReceive().ConfirmAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>());
        viewModel.StatusMessage.Should().Be("attendance.sessionRestored");
    }

    [Fact]
    public async Task Starting_a_session_moves_it_in_progress()
    {
        var harness = new AttendanceVmHarness();
        harness.Sessions
            .GetListAsync(Arg.Any<SessionListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<SessionListItem>>.Success(
                new[] { AttendanceVmHarness.SessionItem() }));

        using var viewModel = harness.CreateAttendance();
        await viewModel.EnsureLoadedAsync();

        await viewModel.StartSessionCommand.ExecuteAsync(viewModel.Items[0]);

        await harness.Sessions.Received(1).SetStatusAsync(
            4,
            SessionStatuses.InProgress,
            null,
            Arg.Any<CancellationToken>());
        viewModel.StatusMessage.Should().Be("attendance.sessionUpdated");
    }

    [Fact]
    public async Task Paying_an_extra_session_writes_an_extra_payment_row()
    {
        var harness = new AttendanceVmHarness();
        harness.Sessions
            .GetListAsync(Arg.Any<SessionListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<SessionListItem>>.Success(
                new[] { ExtraSession(price: 1000, paid: 0, remaining: 1000) }));

        using var viewModel = harness.CreateAttendance();
        await viewModel.EnsureLoadedAsync();

        viewModel.OpenPayCommand.Execute(viewModel.Items[0]);

        viewModel.IsPaymentOverlayOpen.Should().BeTrue();
        viewModel.IsPaidMode.Should().BeTrue();
        viewModel.PaymentOverlayTitle.Should().Be("attendance.pay");
        viewModel.PaymentAmount.Should().Be("1000");

        await viewModel.ConfirmPaymentCommand.ExecuteAsync(null);

        await harness.Payments.Received(1).AddAsync(
            Arg.Is<SchoolManagement.Domain.Entities.Payment>(payment =>
                payment.StudentId == 0
                && payment.GroupId == 1
                && payment.SessionId == 4
                && payment.Type == PaymentTypes.Extra
                && payment.Amount == 1000),
            Arg.Any<CancellationToken>());
        await harness.Sessions.Received(1).UpdatePaymentAsync(
            4,
            true,
            PaymentStatuses.Paid,
            1000,
            0,
            Arg.Any<CancellationToken>());
        viewModel.StatusMessage.Should().Be("attendance.paymentSaved");
    }

    [Fact]
    public async Task Exempting_an_extra_session_writes_no_payment_row()
    {
        var harness = new AttendanceVmHarness();
        harness.Sessions
            .GetListAsync(Arg.Any<SessionListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<SessionListItem>>.Success(
                new[] { ExtraSession(price: 1000, paid: 0, remaining: 1000) }));

        using var viewModel = harness.CreateAttendance();
        await viewModel.EnsureLoadedAsync();

        viewModel.OpenExemptCommand.Execute(viewModel.Items[0]);

        viewModel.IsPaidMode.Should().BeFalse();
        viewModel.PaymentOverlayTitle.Should().Be("attendance.exempt");

        await viewModel.ConfirmPaymentCommand.ExecuteAsync(null);

        await harness.Payments.DidNotReceive().AddAsync(
            Arg.Any<SchoolManagement.Domain.Entities.Payment>(),
            Arg.Any<CancellationToken>());
        await harness.Sessions.Received(1).UpdatePaymentAsync(
            4,
            false,
            PaymentStatuses.Exempted,
            0,
            0,
            Arg.Any<CancellationToken>());
        viewModel.StatusMessage.Should().Be("attendance.paymentSaved");
    }

    [Fact]
    public async Task A_closed_session_cannot_open_the_attendance_screen()
    {
        var harness = new AttendanceVmHarness();
        harness.Sessions
            .GetListAsync(Arg.Any<SessionListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<SessionListItem>>.Success(
                new[] { AttendanceVmHarness.SessionItem(status: SessionStatuses.Completed) }));

        using var viewModel = harness.CreateAttendance();
        await viewModel.EnsureLoadedAsync();

        await viewModel.TakeAttendanceCommand.ExecuteAsync(viewModel.Items[0]);

        viewModel.ErrorMessage.Should().Be("attendance.sessionClosed");
        harness.Navigation.DidNotReceive().NavigateTo<TakeAttendanceViewModel>();
    }

    [Fact]
    public async Task An_open_session_opens_the_attendance_screen()
    {
        var harness = new AttendanceVmHarness();
        harness.Sessions
            .GetListAsync(Arg.Any<SessionListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<SessionListItem>>.Success(
                new[] { AttendanceVmHarness.SessionItem() }));

        var takeAttendance = harness.CreateTakeAttendance();
        harness.Navigation.NavigateTo<TakeAttendanceViewModel>().Returns(takeAttendance);

        using var viewModel = harness.CreateAttendance();
        await viewModel.EnsureLoadedAsync();

        await viewModel.TakeAttendanceCommand.ExecuteAsync(viewModel.Items[0]);

        harness.Navigation.Received(1).NavigateTo<TakeAttendanceViewModel>();
        takeAttendance.SessionId.Should().Be(4);
        takeAttendance.Rows.Should().HaveCount(2);
    }

    [Fact]
    public async Task The_homework_and_report_shortcuts_open_their_pages()
    {
        var harness = new AttendanceVmHarness();
        var homework = harness.CreateHomework();
        var report = harness.CreateReport();
        harness.Navigation.NavigateTo<HomeworkViewModel>().Returns(homework);
        harness.Navigation.NavigateTo<AttendanceReportViewModel>().Returns(report);

        using var viewModel = harness.CreateAttendance();
        await viewModel.EnsureLoadedAsync();

        await viewModel.OpenHomeworksCommand.ExecuteAsync(null);
        await viewModel.OpenReportCommand.ExecuteAsync(null);

        harness.Navigation.Received(1).NavigateTo<HomeworkViewModel>();
        harness.Navigation.Received(1).NavigateTo<AttendanceReportViewModel>();
    }

    [Fact]
    public void Row_flags_follow_the_stored_status_and_payment()
    {
        var harness = new AttendanceVmHarness();

        var extra = SessionRow.Create(ExtraSession(price: 1000, paid: 0, remaining: 1000), harness.Localization);
        extra.IsExtraSession.Should().BeTrue();
        extra.HasPrice.Should().BeTrue();
        extra.CanPay.Should().BeTrue();
        extra.CanTakeAttendance.Should().BeTrue();
        extra.CanCancel.Should().BeTrue();
        extra.CanPostpone.Should().BeTrue();
        extra.PaymentText.Should().Be("attendance.unpaid");

        var paid = SessionRow.Create(ExtraSession(price: 1000, paid: 1000, remaining: 0, paidFlag: true), harness.Localization);
        paid.CanPay.Should().BeFalse();
        paid.PaymentText.Should().Be("attendance.paid");

        var exempted = SessionRow.Create(
            ExtraSession(price: 1000, paid: 0, remaining: 1000, paymentStatus: PaymentStatuses.Exempted),
            harness.Localization);
        exempted.IsExempted.Should().BeTrue();
        exempted.CanPay.Should().BeFalse();
        exempted.PaymentText.Should().Be("attendance.exempt");

        var completed = SessionRow.Create(
            AttendanceVmHarness.SessionItem(status: SessionStatuses.Completed),
            harness.Localization);
        completed.IsCompleted.Should().BeTrue();
        completed.CanTakeAttendance.Should().BeFalse();
        completed.CanCancel.Should().BeFalse();
        completed.CanPostpone.Should().BeFalse();

        var cancelled = SessionRow.Create(
            AttendanceVmHarness.SessionItem(status: SessionStatuses.Cancelled, cancelReason: "Weather"),
            harness.Localization);
        cancelled.IsCancelled.Should().BeTrue();
        cancelled.HasCancelReason.Should().BeTrue();
        cancelled.CancelReasonText.Should().Be("attendance.cancelReasonDisplay");
    }

    [Fact]
    public async Task A_language_change_rebuilds_the_options_and_the_rows()
    {
        var harness = new AttendanceVmHarness();
        harness.Sessions
            .GetListAsync(Arg.Any<SessionListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<SessionListItem>>.Success(
                new[] { AttendanceVmHarness.SessionItem() }));

        using var viewModel = harness.CreateAttendance();
        await viewModel.EnsureLoadedAsync();

        harness.Localization.Values["students.allGroups"] = "All groups";
        harness.Localization.Values["attendance.scheduled"] = "Scheduled";
        harness.Localization.Values["attendance.regular"] = "Regular";
        harness.Localization.Values["attendance.cancelSession"] = "Cancel session";
        harness.Localization.Values["attendance.noSessionsToday"] = "Nothing today";

        viewModel.CancelSessionCommand.Execute(viewModel.Items[0]);
        harness.Localization.RaiseLanguageChanged();

        viewModel.Groups[0].Label.Should().Be("All groups");
        viewModel.Items[0].StatusText.Should().Be("Scheduled");
        viewModel.Items[0].TypeText.Should().Be("Regular");
        viewModel.StatusOverlayTitle.Should().Be("Cancel session");
        viewModel.EmptyMessage.Should().Be("Nothing today");
        viewModel.SessionTypes[0].Label.Should().NotBeEmpty();
    }

    [Fact]
    public void Disposing_detaches_the_language_handler()
    {
        var harness = new AttendanceVmHarness();
        var viewModel = harness.CreateAttendance();
        viewModel.Dispose();

        var notifications = 0;
        viewModel.PropertyChanged += (_, _) => notifications++;

        harness.Localization.Values["students.allGroups"] = "All groups";
        harness.Localization.RaiseLanguageChanged();

        notifications.Should().Be(0);
    }

    private static SessionListItem ExtraSession(
        double price,
        double paid,
        double remaining,
        bool paidFlag = false,
        string? paymentStatus = null) => new(
        4,
        1,
        "Group Alpha",
        Day,
        new DateTime(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 11, 4, 10, 0, 0, DateTimeKind.Utc),
        SessionTypes.Makeup,
        SessionStatuses.Scheduled,
        "Revision",
        paidFlag,
        price,
        paymentStatus,
        paid,
        remaining);
}
