using FluentAssertions;
using NSubstitute;
using SchoolManagement.Application.Attendance;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Groups;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Services;
using SchoolManagement.Application.Time;
using SchoolManagement.Application.UseCases;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Tests.Attendance;

/// <summary>
/// Phase 7 attendance/session use cases. Every write passes the license gate
/// before touching a repository, validation short-circuits before the database,
/// and the status machine is enforced through <c>SessionStatusPolicy</c>.
/// </summary>
public sealed class AttendanceUseCaseTests
{
    private static readonly DateTime Day = new(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc);

    // ChangeSessionStatus ---------------------------------------------------

    [Fact]
    public async Task Changing_a_status_checks_the_license_gate_first()
    {
        var sessions = Substitute.For<ISessionRepository>();
        var guard = Blocking();

        var result = await new ChangeSessionStatusUseCase(sessions, guard)
            .ExecuteAsync(new SessionStatusChangeRequest(7, SessionStatuses.Cancelled, "Holiday"));

        result.Failure!.Kind.Should().Be(FailureKind.License);
        guard.Received(1).EnsureCanWrite();
        await sessions.DidNotReceive().GetSummaryAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_unknown_status_is_rejected_before_the_repository()
    {
        var sessions = Substitute.For<ISessionRepository>();

        var result = await new ChangeSessionStatusUseCase(sessions, Allowing())
            .ExecuteAsync(new SessionStatusChangeRequest(7, "done"));

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Validators.InvalidField);
        await sessions.DidNotReceive().GetSummaryAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_missing_session_reports_not_found()
    {
        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetSummaryAsync(7, Arg.Any<CancellationToken>())
            .Returns(Result<SessionListItem?>.Success(null));

        var result = await new ChangeSessionStatusUseCase(sessions, Allowing())
            .ExecuteAsync(new SessionStatusChangeRequest(7, SessionStatuses.Cancelled));

        result.Failure!.Kind.Should().Be(FailureKind.NotFound);
        result.Failure.MessageKey.Should().Be(TranslationKeys.Attendance.SessionNotFound);
        await sessions.DidNotReceive().SetStatusAsync(
            Arg.Any<int>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_completed_session_cannot_be_cancelled()
    {
        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetSummaryAsync(7, Arg.Any<CancellationToken>())
            .Returns(Result<SessionListItem?>.Success(SessionItem(status: SessionStatuses.Completed)));

        var result = await new ChangeSessionStatusUseCase(sessions, Allowing())
            .ExecuteAsync(new SessionStatusChangeRequest(7, SessionStatuses.Cancelled));

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Validators.InvalidField);
        await sessions.DidNotReceive().SetStatusAsync(
            Arg.Any<int>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancelling_trims_the_reason_and_suggests_an_sms()
    {
        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetSummaryAsync(7, Arg.Any<CancellationToken>())
            .Returns(Result<SessionListItem?>.Success(SessionItem()));
        sessions.SetStatusAsync(
                7,
                SessionStatuses.Cancelled,
                "Official holiday",
                Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));
        var guard = Allowing();

        var result = await new ChangeSessionStatusUseCase(sessions, guard)
            .ExecuteAsync(new SessionStatusChangeRequest(7, SessionStatuses.Cancelled, "  Official holiday  "));

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.Status.Should().Be(SessionStatuses.Cancelled);
        result.Value.SuggestsSms.Should().BeTrue();
        guard.Received(1).EnsureCanWrite();
        await sessions.Received(1).SetStatusAsync(
            7,
            SessionStatuses.Cancelled,
            "Official holiday",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Restoring_a_postponed_session_does_not_suggest_an_sms()
    {
        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetSummaryAsync(7, Arg.Any<CancellationToken>())
            .Returns(Result<SessionListItem?>.Success(SessionItem(status: SessionStatuses.Postponed)));
        sessions.SetStatusAsync(
                7,
                SessionStatuses.Scheduled,
                null,
                Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        var result = await new ChangeSessionStatusUseCase(sessions, Allowing())
            .ExecuteAsync(new SessionStatusChangeRequest(7, SessionStatuses.Scheduled));

        result.Value.SuggestsSms.Should().BeFalse();
        await sessions.Received(1).SetStatusAsync(
            7,
            SessionStatuses.Scheduled,
            null,
            Arg.Any<CancellationToken>());
    }

    // CreateExtraSession ----------------------------------------------------

    [Fact]
    public async Task Creating_an_extra_session_checks_the_license_gate_first()
    {
        var sessions = Substitute.For<ISessionRepository>();
        var guard = Blocking();

        var result = await new CreateExtraSessionUseCase(sessions, guard).ExecuteAsync(Extra());

        result.Failure!.Kind.Should().Be(FailureKind.License);
        guard.Received(1).EnsureCanWrite();
        await sessions.DidNotReceive().CreateExtraAsync(
            Arg.Any<ExtraSessionRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_inverted_time_range_is_rejected_before_the_repository()
    {
        var sessions = Substitute.For<ISessionRepository>();

        var result = await new CreateExtraSessionUseCase(sessions, Allowing())
            .ExecuteAsync(Extra(endTime: TimeSpan.FromHours(8)));

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Attendance.InvalidTimeRange);
        await sessions.DidNotReceive().CreateExtraAsync(
            Arg.Any<ExtraSessionRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(SessionTypes.Regular)]
    [InlineData("lesson")]
    public async Task Only_makeup_or_extra_sessions_can_be_created(string type)
    {
        var sessions = Substitute.For<ISessionRepository>();

        var result = await new CreateExtraSessionUseCase(sessions, Allowing())
            .ExecuteAsync(Extra() with { Type = type });

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Validators.InvalidField);
        await sessions.DidNotReceive().CreateExtraAsync(
            Arg.Any<ExtraSessionRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_negative_price_is_rejected()
    {
        var sessions = Substitute.For<ISessionRepository>();

        var result = await new CreateExtraSessionUseCase(sessions, Allowing())
            .ExecuteAsync(Extra() with { Price = -1 });

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Validators.PositiveNumber);
    }

    [Fact]
    public async Task Creating_an_extra_session_reports_the_existing_row_instead_of_a_duplicate()
    {
        var sessions = Substitute.For<ISessionRepository>();
        sessions.CreateExtraAsync(Arg.Any<ExtraSessionRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<ExtraSessionOutcome>.Success(new ExtraSessionOutcome(42, false)));

        var result = await new CreateExtraSessionUseCase(sessions, Allowing()).ExecuteAsync(Extra());

        result.IsSuccess.Should().BeTrue();
        result.Value.SessionId.Should().Be(42);
        result.Value.Created.Should().BeFalse();
    }

    [Fact]
    public async Task Creating_an_extra_session_propagates_a_repository_failure()
    {
        var sessions = Substitute.For<ISessionRepository>();
        sessions.CreateExtraAsync(Arg.Any<ExtraSessionRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<ExtraSessionOutcome>.Fail(Failure.Database()));

        var result = await new CreateExtraSessionUseCase(sessions, Allowing()).ExecuteAsync(Extra());

        result.Failure!.Kind.Should().Be(FailureKind.Database);
    }

    // SaveSessionAttendance ------------------------------------------------

    [Fact]
    public async Task Saving_attendance_checks_the_license_gate_first()
    {
        var sessions = Substitute.For<ISessionRepository>();
        var attendance = Substitute.For<IAttendanceRepository>();
        var notifications = Substitute.For<INotificationRepository>();
        var guard = Blocking();

        var result = await new SaveSessionAttendanceUseCase(
                sessions,
                attendance,
                QuietNotifications(notifications),
                guard)
            .ExecuteAsync(Bulk());

        result.Failure!.Kind.Should().Be(FailureKind.License);
        guard.Received(1).EnsureCanWrite();
        await attendance.DidNotReceive().UpsertManyAsync(
            Arg.Any<int>(),
            Arg.Any<IReadOnlyList<AttendanceEntry>>(),
            Arg.Any<CancellationToken>());
        await notifications.DidNotReceive().AddAsync(
            Arg.Any<Notification>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_empty_attendance_save_is_rejected_before_the_repository()
    {
        var attendance = Substitute.For<IAttendanceRepository>();

        var result = await new SaveSessionAttendanceUseCase(
                Substitute.For<ISessionRepository>(),
                attendance,
                QuietNotifications(),
                Allowing())
            .ExecuteAsync(new SaveSessionAttendanceRequest(7, []));

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Errors.RequiredFields);
        await attendance.DidNotReceive().UpsertManyAsync(
            Arg.Any<int>(),
            Arg.Any<IReadOnlyList<AttendanceEntry>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_closed_session_refuses_new_attendance()
    {
        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetSummaryAsync(7, Arg.Any<CancellationToken>())
            .Returns(Result<SessionListItem?>.Success(SessionItem(status: SessionStatuses.Completed)));
        var attendance = Substitute.For<IAttendanceRepository>();

        var result = await new SaveSessionAttendanceUseCase(
                sessions,
                attendance,
                QuietNotifications(),
                Allowing())
            .ExecuteAsync(Bulk());

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Attendance.SessionClosed);
        await attendance.DidNotReceive().UpsertManyAsync(
            Arg.Any<int>(),
            Arg.Any<IReadOnlyList<AttendanceEntry>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Saving_the_roster_writes_the_records_then_completes_the_session()
    {
        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetSummaryAsync(7, Arg.Any<CancellationToken>())
            .Returns(Result<SessionListItem?>.Success(SessionItem()));
        sessions.SetStatusAsync(
                7,
                SessionStatuses.Completed,
                null,
                Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));
        var attendance = Substitute.For<IAttendanceRepository>();
        attendance.UpsertManyAsync(7, Arg.Any<IReadOnlyList<AttendanceEntry>>(), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(2));

        var result = await new SaveSessionAttendanceUseCase(
                sessions,
                attendance,
                QuietNotifications(),
                Allowing())
            .ExecuteAsync(Bulk());

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.SavedCount.Should().Be(2);
        result.Value.AbsentCount.Should().Be(1);
        result.Value.NotificationsCreated.Should().Be(0);
        await attendance.Received(1).UpsertManyAsync(
            7,
            Arg.Is<IReadOnlyList<AttendanceEntry>>(entries => entries.Count == 2),
            Arg.Any<CancellationToken>());
        await sessions.Received(1).SetStatusAsync(
            7,
            SessionStatuses.Completed,
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Saving_attendance_counts_unexcused_as_an_absence_and_uses_the_limit()
    {
        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetSummaryAsync(7, Arg.Any<CancellationToken>())
            .Returns(Result<SessionListItem?>.Success(SessionItem()));
        sessions.SetStatusAsync(
                7,
                SessionStatuses.Completed,
                null,
                Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));
        var attendance = Substitute.For<IAttendanceRepository>();
        attendance.UpsertManyAsync(7, Arg.Any<IReadOnlyList<AttendanceEntry>>(), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(2));

        var students = Substitute.For<IStudentRepository>();
        students.GetAbsenceSummaryAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<StudentAbsenceSummary>.Success(new StudentAbsenceSummary(1, 5, 5, true, false)));
        students.GetDetailsAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<StudentDetails?>.Success(Details(1)));
        var notifications = Substitute.For<INotificationRepository>();
        notifications.HasUnreadAsync(
                NotificationTypes.Attendance,
                1,
                Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));
        notifications.AddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(11));

        var result = await new SaveSessionAttendanceUseCase(
                sessions,
                attendance,
                Notifications(students, notifications),
                Allowing())
            .ExecuteAsync(new SaveSessionAttendanceRequest(
                7,
                [
                    new AttendanceEntry(1, AttendanceStatuses.Unexcused),
                    new AttendanceEntry(2, AttendanceStatuses.Present),
                ]));

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.AbsentCount.Should().Be(1);
        result.Value.NotificationsCreated.Should().Be(1);
        await notifications.Received(1).AddAsync(
            Arg.Is<Notification>(notification =>
                notification.Type == NotificationTypes.Attendance
                && notification.RecipientId == 1
                && notification.RecipientType == RecipientTypes.Student
                && notification.Channel == NotificationChannels.InApp),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_notification_failure_never_fails_the_saved_attendance()
    {
        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetSummaryAsync(7, Arg.Any<CancellationToken>())
            .Returns(Result<SessionListItem?>.Success(SessionItem()));
        sessions.SetStatusAsync(
                7,
                SessionStatuses.Completed,
                null,
                Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));
        var attendance = Substitute.For<IAttendanceRepository>();
        attendance.UpsertManyAsync(7, Arg.Any<IReadOnlyList<AttendanceEntry>>(), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(1));

        var students = Substitute.For<IStudentRepository>();
        students.GetAbsenceSummaryAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<StudentAbsenceSummary>.Fail(Failure.Database()));

        var result = await new SaveSessionAttendanceUseCase(
                sessions,
                attendance,
                Notifications(students, Substitute.For<INotificationRepository>()),
                Allowing())
            .ExecuteAsync(new SaveSessionAttendanceRequest(
                7,
                [new AttendanceEntry(1, AttendanceStatuses.Absent)]));

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.SavedCount.Should().Be(1);
        result.Value.NotificationsCreated.Should().Be(0);
    }

    // SetSessionPayment -----------------------------------------------------

    [Fact]
    public async Task Paying_an_extra_session_checks_the_license_gate_first()
    {
        var sessions = Substitute.For<ISessionRepository>();
        var payments = Substitute.For<IPaymentRepository>();
        var guard = Blocking();

        var result = await new SetSessionPaymentUseCase(sessions, payments, Clock(), guard)
            .ExecuteAsync(new SessionPaymentRequest(7, SessionPaymentModes.Paid, 500));

        result.Failure!.Kind.Should().Be(FailureKind.License);
        guard.Received(1).EnsureCanWrite();
        await payments.DidNotReceive().AddAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("free")]
    [InlineData("")]
    public async Task An_unknown_payment_mode_is_rejected(string mode)
    {
        var sessions = Substitute.For<ISessionRepository>();

        var result = await new SetSessionPaymentUseCase(
                sessions,
                Substitute.For<IPaymentRepository>(),
                Clock(),
                Allowing())
            .ExecuteAsync(new SessionPaymentRequest(7, mode));

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Validators.InvalidField);
        await sessions.DidNotReceive().UpdatePaymentAsync(
            Arg.Any<int>(),
            Arg.Any<bool>(),
            Arg.Any<string?>(),
            Arg.Any<double>(),
            Arg.Any<double>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Exempting_a_session_only_writes_the_payment_status()
    {
        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetSummaryAsync(7, Arg.Any<CancellationToken>())
            .Returns(Result<SessionListItem?>.Success(SessionItem()));
        sessions.UpdatePaymentAsync(
                7,
                false,
                PaymentStatuses.Exempted,
                0,
                0,
                Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));
        var payments = Substitute.For<IPaymentRepository>();

        var result = await new SetSessionPaymentUseCase(sessions, payments, Clock(), Allowing())
            .ExecuteAsync(new SessionPaymentRequest(7, SessionPaymentModes.Exempted));

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.PaymentId.Should().BeNull();
        result.Value.PaymentStatus.Should().Be(PaymentStatuses.Exempted);
        await payments.DidNotReceive().AddAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Paying_a_session_writes_a_group_level_extra_payment()
    {
        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetSummaryAsync(7, Arg.Any<CancellationToken>())
            .Returns(Result<SessionListItem?>.Success(SessionItem(price: 800)));
        sessions.UpdatePaymentAsync(
                7,
                true,
                PaymentStatuses.Paid,
                800,
                0,
                Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));
        var payments = Substitute.For<IPaymentRepository>();
        Payment? stored = null;
        payments.AddAsync(Arg.Do<Payment>(payment => stored = payment), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(99));

        var result = await new SetSessionPaymentUseCase(sessions, payments, Clock(), Allowing())
            .ExecuteAsync(new SessionPaymentRequest(7, SessionPaymentModes.Paid));

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.PaymentId.Should().Be(99);
        result.Value.PaymentStatus.Should().Be(PaymentStatuses.Paid);
        stored.Should().NotBeNull();
        stored!.StudentId.Should().Be(0);
        stored.GroupId.Should().Be(3);
        stored.SessionId.Should().Be(7);
        stored.Type.Should().Be(PaymentTypes.Extra);
        stored.Amount.Should().Be(800);
        stored.RemainingAmount.Should().Be(0);
        stored.ReceiptNumber.Should().StartWith("SES-7-");
        await sessions.Received(1).UpdatePaymentAsync(
            7,
            true,
            PaymentStatuses.Paid,
            800,
            0,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_partial_payment_keeps_the_remaining_amount()
    {
        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetSummaryAsync(7, Arg.Any<CancellationToken>())
            .Returns(Result<SessionListItem?>.Success(SessionItem(price: 800, paidAmount: 200)));
        sessions.UpdatePaymentAsync(
                7,
                false,
                PaymentStatuses.Partial,
                500,
                300,
                Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));
        var payments = Substitute.For<IPaymentRepository>();
        payments.AddAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(100));

        var result = await new SetSessionPaymentUseCase(sessions, payments, Clock(), Allowing())
            .ExecuteAsync(new SessionPaymentRequest(7, SessionPaymentModes.Paid, 300));

        result.Value.PaymentStatus.Should().Be(PaymentStatuses.Partial);
        await sessions.Received(1).UpdatePaymentAsync(
            7,
            false,
            PaymentStatuses.Partial,
            500,
            300,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Paying_a_priceless_session_without_an_amount_is_rejected()
    {
        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetSummaryAsync(7, Arg.Any<CancellationToken>())
            .Returns(Result<SessionListItem?>.Success(SessionItem(price: null)));

        var result = await new SetSessionPaymentUseCase(
                sessions,
                Substitute.For<IPaymentRepository>(),
                Clock(),
                Allowing())
            .ExecuteAsync(new SessionPaymentRequest(7, SessionPaymentModes.Paid));

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Validators.PositiveNumber);
    }

    // SendSessionSms --------------------------------------------------------

    [Fact]
    public async Task Sending_the_session_sms_checks_the_license_gate_first()
    {
        var sessions = Substitute.For<ISessionRepository>();
        var sms = Substitute.For<ISmsService>();
        var guard = Blocking();

        var result = await new SendSessionSmsUseCase(sessions, sms, Localization(), guard)
            .ExecuteAsync(new SessionSmsRequest(7, SessionStatuses.Cancelled));

        result.Failure!.Kind.Should().Be(FailureKind.License);
        guard.Received(1).EnsureCanWrite();
        await sms.DidNotReceive().SendAsync(Arg.Any<SmsSendRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_unknown_sms_event_is_rejected_without_a_query()
    {
        var sessions = Substitute.For<ISessionRepository>();

        var result = await new SendSessionSmsUseCase(
                sessions,
                Substitute.For<ISmsService>(),
                Localization(),
                Allowing())
            .ExecuteAsync(new SessionSmsRequest(7, "party"));

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Validators.InvalidField);
        await sessions.DidNotReceive().GetSummaryAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task One_sms_log_is_written_per_guardian_phone()
    {
        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetSummaryAsync(7, Arg.Any<CancellationToken>())
            .Returns(Result<SessionListItem?>.Success(
                SessionItem(status: SessionStatuses.Cancelled, cancelReason: "Holiday")));
        sessions.GetGuardianContactsAsync(3, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<GuardianContact>>.Success(
            [
                new GuardianContact(1, "Student One", "Parent One", "+213555000001"),
                new GuardianContact(2, "Student Two", "Parent Two", " "),
                new GuardianContact(3, "Student Three", null, "+213555000003"),
            ]));
        var sms = Substitute.For<ISmsService>();
        sms.SendAsync(Arg.Any<SmsSendRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => Result<SmsSendResult>.Success(
                new SmsSendResult(1, call.Arg<SmsSendRequest>().Phone, call.Arg<SmsSendRequest>().Body)));

        var result = await new SendSessionSmsUseCase(sessions, sms, Localization(), Allowing())
            .ExecuteAsync(new SessionSmsRequest(7, SessionStatuses.Cancelled));

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.LogCount.Should().Be(2);
        result.Value.Phones.Should().Equal("+213555000001", "+213555000003");
        await sms.Received(2).SendAsync(Arg.Any<SmsSendRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_group_without_a_guardian_phone_reports_the_missing_number()
    {
        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetSummaryAsync(7, Arg.Any<CancellationToken>())
            .Returns(Result<SessionListItem?>.Success(SessionItem()));
        sessions.GetGuardianContactsAsync(3, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<GuardianContact>>.Success(
            [
                new GuardianContact(1, "Student One", null, null),
            ]));

        var result = await new SendSessionSmsUseCase(
                sessions,
                Substitute.For<ISmsService>(),
                Localization(),
                Allowing())
            .ExecuteAsync(new SessionSmsRequest(7, SessionStatuses.Postponed));

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Notifications.NoPhoneNumber);
    }

    [Fact]
    public async Task A_failed_sms_log_stops_the_sweep()
    {
        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetSummaryAsync(7, Arg.Any<CancellationToken>())
            .Returns(Result<SessionListItem?>.Success(SessionItem()));
        sessions.GetGuardianContactsAsync(3, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<GuardianContact>>.Success(
            [
                new GuardianContact(1, "Student One", null, "+213555000001"),
                new GuardianContact(2, "Student Two", null, "+213555000002"),
            ]));
        var sms = Substitute.For<ISmsService>();
        sms.SendAsync(Arg.Any<SmsSendRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<SmsSendResult>.Fail(Failure.Database()));

        var result = await new SendSessionSmsUseCase(sessions, sms, Localization(), Allowing())
            .ExecuteAsync(new SessionSmsRequest(7, SessionStatuses.Cancelled));

        result.Failure!.Kind.Should().Be(FailureKind.Database);
        await sms.Received(1).SendAsync(Arg.Any<SmsSendRequest>(), Arg.Any<CancellationToken>());
    }

    // Read use cases --------------------------------------------------------

    [Fact]
    public async Task The_attendance_board_joins_the_session_the_roster_and_due_homeworks()
    {
        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetSummaryAsync(7, Arg.Any<CancellationToken>())
            .Returns(Result<SessionListItem?>.Success(SessionItem()));
        var attendance = Substitute.For<IAttendanceRepository>();
        attendance.GetRosterAsync(7, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<AttendanceRosterItem>>.Success(
            [
                new AttendanceRosterItem(1, "STU-2026-0001", "Student One", AttendanceStatuses.Present, null, null),
            ]));
        var homeworks = Substitute.For<IHomeworkRepository>();
        homeworks.GetDueForSessionAsync(7, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HomeworkListItem>>.Success(
            [
                new HomeworkListItem(1, 3, 6, 7, "Exercise", "Page 10", Day, false, 1, 0, 1),
            ]));

        var result = await new GetSessionAttendanceBoardUseCase(sessions, attendance, homeworks)
            .ExecuteAsync(7);

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.Session.Id.Should().Be(7);
        result.Value.Roster.Should().ContainSingle();
        result.Value.DueHomeworks.Should().ContainSingle();
    }

    [Fact]
    public async Task The_attendance_board_reports_a_missing_session()
    {
        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetSummaryAsync(7, Arg.Any<CancellationToken>())
            .Returns(Result<SessionListItem?>.Success(null));

        var result = await new GetSessionAttendanceBoardUseCase(
                sessions,
                Substitute.For<IAttendanceRepository>(),
                Substitute.For<IHomeworkRepository>())
            .ExecuteAsync(7);

        result.Failure!.Kind.Should().Be(FailureKind.NotFound);
        result.Failure.MessageKey.Should().Be(TranslationKeys.Attendance.SessionNotFound);
    }

    [Fact]
    public async Task The_attendance_board_propagates_a_roster_failure()
    {
        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetSummaryAsync(7, Arg.Any<CancellationToken>())
            .Returns(Result<SessionListItem?>.Success(SessionItem()));
        var attendance = Substitute.For<IAttendanceRepository>();
        attendance.GetRosterAsync(7, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<AttendanceRosterItem>>.Fail(Failure.Database()));

        var result = await new GetSessionAttendanceBoardUseCase(
                sessions,
                attendance,
                Substitute.For<IHomeworkRepository>())
            .ExecuteAsync(7);

        result.Failure!.Kind.Should().Be(FailureKind.Database);
    }

    [Fact]
    public async Task The_attendance_report_rejects_an_inverted_period()
    {
        var attendance = Substitute.For<IAttendanceRepository>();

        var result = await new GetAttendanceReportUseCase(
                attendance,
                Substitute.For<IReferenceDataRepository>(),
                Substitute.For<IGroupRepository>(),
                Clock())
            .ExecuteAsync(new AttendanceReportQuery(Day.AddDays(1), Day));

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Validators.InvalidDate);
        await attendance.DidNotReceive().GetReportAsync(
            Arg.Any<AttendanceReportQuery>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_attendance_report_builds_the_period_bundle()
    {
        var attendance = Substitute.For<IAttendanceRepository>();
        attendance.GetReportAsync(Arg.Any<AttendanceReportQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<AttendanceReportRow>>.Success(
            [
                new AttendanceReportRow(1, "STU-2026-0001", "Student One", 3, "Group One", 4, 3, 1, 0),
                new AttendanceReportRow(2, "STU-2026-0002", "Student Two", 3, "Group One", 4, 2, 1, 1),
            ]));
        var references = Substitute.For<IReferenceDataRepository>();
        references.GetSchoolNameAsync(Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("School One"));
        var groups = Substitute.For<IGroupRepository>();
        groups.GetSummaryAsync(3, Arg.Any<CancellationToken>())
            .Returns(Result<GroupSummary?>.Success(
                new GroupSummary(3, "Group One", 1, "Math", 10, "Teacher", 2, "Level", 3, "Grade", 30, 100, true, 2)));

        var result = await new GetAttendanceReportUseCase(attendance, references, groups, Clock())
            .ExecuteAsync(new AttendanceReportQuery(Day, Day.AddDays(30), StudentId: 1, GroupId: 3));

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.SchoolName.Should().Be("School One");
        result.Value.GroupName.Should().Be("Group One");
        result.Value.StudentName.Should().Be("Student One");
        result.Value.TotalRecords.Should().Be(8);
        result.Value.TotalPresent.Should().Be(5);
        result.Value.TotalAbsent.Should().Be(2);
        result.Value.TotalLate.Should().Be(1);
        result.Value.AttendanceRate.Should().Be(75d);
    }

    [Fact]
    public async Task The_attendance_report_propagates_a_row_failure()
    {
        var attendance = Substitute.For<IAttendanceRepository>();
        attendance.GetReportAsync(Arg.Any<AttendanceReportQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<AttendanceReportRow>>.Fail(Failure.Database()));

        var result = await new GetAttendanceReportUseCase(
                attendance,
                Substitute.For<IReferenceDataRepository>(),
                Substitute.For<IGroupRepository>(),
                Clock())
            .ExecuteAsync(new AttendanceReportQuery(Day, Day));

        result.Failure!.Kind.Should().Be(FailureKind.Database);
    }

    // Helpers ---------------------------------------------------------------

    private static SessionListItem SessionItem(
        int id = 7,
        string status = SessionStatuses.Scheduled,
        string type = SessionTypes.Regular,
        double? price = 500,
        double paidAmount = 0,
        string? cancelReason = null) => new(
        id,
        3,
        "Group One",
        Day,
        new DateTime(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 11, 4, 10, 0, 0, DateTimeKind.Utc),
        type,
        status,
        "Algebra",
        false,
        price,
        PaymentStatuses.Unpaid,
        paidAmount,
        price is { } value ? Math.Max(0, value - paidAmount) : 0,
        cancelReason);

    private static ExtraSessionRequest Extra(TimeSpan? endTime = null) => new(
        3,
        Day,
        TimeSpan.FromHours(9),
        endTime ?? TimeSpan.FromHours(10),
        SessionTypes.Extra,
        "Revision",
        null,
        500);

    private static SaveSessionAttendanceRequest Bulk() => new(
        7,
        [
            new AttendanceEntry(1, AttendanceStatuses.Present),
            new AttendanceEntry(2, AttendanceStatuses.Absent),
        ]);

    private static StudentDetails Details(int id) => new(
        id,
        $"STU-2026-{id:0000}",
        $"First{id}",
        $"Last{id}",
        null,
        null,
        null,
        null,
        null,
        StudentStatuses.Active,
        null,
        0,
        null,
        null,
        null);

    private static AutoNotificationService QuietNotifications(INotificationRepository? notifications = null)
    {
        var students = Substitute.For<IStudentRepository>();
        students.GetAbsenceSummaryAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => Result<StudentAbsenceSummary>.Success(
                new StudentAbsenceSummary(call.Arg<int>(), 0, 5, false, false)));
        return Notifications(students, notifications ?? Substitute.For<INotificationRepository>());
    }

    private static AutoNotificationService Notifications(
        IStudentRepository students,
        INotificationRepository notifications)
        => new(students, notifications, Localization(), Clock());

    private static ILocalizationService Localization()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.Tr(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns(call => call.Arg<string>());
        return localization;
    }

    private static ITrustedClock Clock()
    {
        var clock = Substitute.For<ITrustedClock>();
        clock.LocalNow.Returns(new DateTime(2026, 11, 4, 10, 30, 0, DateTimeKind.Utc));
        clock.UtcNow.Returns(new DateTime(2026, 11, 4, 9, 30, 0, DateTimeKind.Utc));
        return clock;
    }

    private static ILicenseGuard Allowing()
    {
        var guard = Substitute.For<ILicenseGuard>();
        guard.EnsureCanWrite().Returns(Result<bool>.Success(true));
        return guard;
    }

    private static ILicenseGuard Blocking()
    {
        var guard = Substitute.For<ILicenseGuard>();
        guard.EnsureCanWrite().Returns(Result<bool>.Fail(
            Failure.License(TranslationKeys.License.ErrorWriteBlocked)));
        return guard;
    }
}
