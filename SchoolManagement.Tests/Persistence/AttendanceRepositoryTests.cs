using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolManagement.Application.Attendance;
using SchoolManagement.Domain.Common;
using SchoolManagement.Infrastructure.Persistence.Converters;
using SchoolManagement.Infrastructure.Persistence.Repositories;

namespace SchoolManagement.Tests.Persistence;

/// <summary>
/// Real SQLite coverage of the Phase 7 session/attendance repositories: the
/// deliberate list projection and filters, the "unmarked = present" roster, the
/// bulk upsert, idempotent extra sessions, the grouped report and the guardian
/// contacts used by the SMS suggestion.
/// </summary>
public sealed class AttendanceRepositoryTests
{
    private static readonly DateTime Day = new(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task The_session_list_projects_the_group_and_honours_every_filter()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateSessionRepository(host);

        var all = await repository.GetListAsync(new SessionListQuery());
        all.IsSuccess.Should().BeTrue(all.Failure?.Detail);
        all.Value.Select(session => session.Id).Should().ContainInOrder(4, 1, 3, 2);
        all.Value[0].GroupName.Should().Be("Group Alpha");
        all.Value[0].SessionDate.Should().Be(Day.AddDays(1));

        var day = await repository.GetListAsync(new SessionListQuery(FromDate: Day, ToDate: Day));
        day.Value.Select(session => session.Id).Should().ContainInOrder(1, 3);

        var group = await repository.GetListAsync(new SessionListQuery(GroupId: 1));
        group.Value.Select(session => session.Id).Should().ContainInOrder(4, 1, 2);

        var cancelled = await repository.GetListAsync(new SessionListQuery(Status: SessionStatuses.Cancelled));
        cancelled.Value.Should().ContainSingle().Which.Id.Should().Be(2);

        var extra = await repository.GetListAsync(new SessionListQuery(Type: SessionTypes.Extra));
        var session = extra.Value.Should().ContainSingle().Subject;
        session.Id.Should().Be(3);
        session.GroupName.Should().Be("Group Beta");
        session.Price.Should().Be(500);
        session.PaymentStatus.Should().Be(PaymentStatuses.Unpaid);
    }

    [Fact]
    public async Task The_summary_reports_a_missing_session()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateSessionRepository(host);

        var result = await repository.GetSummaryAsync(999);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task The_roster_defaults_unmarked_students_to_present()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateAttendanceRepository(host);

        // Session 2 has no attendance row at all, so every active student of the
        // group must come back as "present" instead of missing from the grid.
        var result = await repository.GetRosterAsync(2);

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.Should().HaveCount(3);
        result.Value.Should().OnlyContain(row => row.Status == AttendanceStatuses.Present);
        result.Value.Select(row => row.StudentName)
            .Should().ContainInOrder("Alice Anderson", "Bob Brown", "Eve Evans");
    }

    [Fact]
    public async Task The_roster_keeps_saved_statuses_and_excludes_withdrawn_students()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateAttendanceRepository(host);

        var result = await repository.GetRosterAsync(1);

        result.Value.Should().HaveCount(3);
        result.Value.Single(row => row.StudentId == 1).Status.Should().Be(AttendanceStatuses.Present);
        result.Value.Single(row => row.StudentId == 2).Status.Should().Be(AttendanceStatuses.Absent);
        result.Value.Single(row => row.StudentId == 5).Status.Should().Be(AttendanceStatuses.Present);
        result.Value.Should().NotContain(row => row.StudentId == 3);
    }

    [Fact]
    public async Task Bulk_upsert_updates_existing_rows_without_creating_duplicates()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateAttendanceRepository(host);

        var saved = await repository.UpsertManyAsync(
            1,
            [
                new AttendanceEntry(1, AttendanceStatuses.Absent),
                new AttendanceEntry(2, AttendanceStatuses.Present),
                new AttendanceEntry(5, AttendanceStatuses.Late),
            ]);

        saved.IsSuccess.Should().BeTrue(saved.Failure?.Detail);
        saved.Value.Should().Be(3);

        await using var connection = await host.OpenAsync();
        (await DatabaseTestHost.LongAsync(connection, "SELECT COUNT(*) FROM attendance WHERE session_id = 1"))
            .Should().Be(3);

        var roster = await repository.GetRosterAsync(1);
        var first = roster.Value.Single(row => row.StudentId == 1);
        first.Status.Should().Be(AttendanceStatuses.Absent);
        first.CheckInTime.Should().BeNull();

        var second = roster.Value.Single(row => row.StudentId == 2);
        second.Status.Should().Be(AttendanceStatuses.Present);
        second.CheckInTime.Should().NotBeNull();

        roster.Value.Single(row => row.StudentId == 5).Status.Should().Be(AttendanceStatuses.Late);
    }

    [Fact]
    public async Task Bulk_upsert_creates_a_row_for_an_unmarked_student()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateAttendanceRepository(host);

        var saved = await repository.UpsertManyAsync(
            2,
            [new AttendanceEntry(4, AttendanceStatuses.Present)]);

        saved.IsSuccess.Should().BeTrue(saved.Failure?.Detail);

        await using var connection = await host.OpenAsync();
        (await DatabaseTestHost.LongAsync(connection, "SELECT COUNT(*) FROM attendance WHERE session_id = 2"))
            .Should().Be(1);
    }

    [Fact]
    public async Task Creating_an_extra_session_is_idempotent()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateSessionRepository(host);
        var request = new ExtraSessionRequest(
            1,
            Day,
            TimeSpan.FromHours(14),
            TimeSpan.FromHours(15),
            SessionTypes.Extra,
            "Revision",
            null,
            700);

        var first = await repository.CreateExtraAsync(request);
        var second = await repository.CreateExtraAsync(request);

        first.IsSuccess.Should().BeTrue(first.Failure?.Detail);
        first.Value.Created.Should().BeTrue();
        second.IsSuccess.Should().BeTrue(second.Failure?.Detail);
        second.Value.Created.Should().BeFalse();
        second.Value.SessionId.Should().Be(first.Value.SessionId);

        await using var connection = await host.OpenAsync();
        (await DatabaseTestHost.LongAsync(
                connection,
                $"SELECT COUNT(*) FROM sessions WHERE group_id = 1 AND type = 'extra'"))
            .Should().Be(1);

        var summary = await repository.GetSummaryAsync(first.Value.SessionId);
        summary.Value!.Type.Should().Be(SessionTypes.Extra);
        summary.Value.Status.Should().Be(SessionStatuses.Scheduled);
        summary.Value.Price.Should().Be(700);
        summary.Value.PaymentStatus.Should().Be(PaymentStatuses.Unpaid);
        summary.Value.RemainingAmount.Should().Be(700);
        summary.Value.LessonTopic.Should().Be("Revision");
    }

    [Fact]
    public async Task An_extra_session_without_a_price_has_no_payment_status()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateSessionRepository(host);

        var created = await repository.CreateExtraAsync(new ExtraSessionRequest(
            2,
            Day.AddDays(3),
            TimeSpan.FromHours(16),
            TimeSpan.FromHours(17),
            SessionTypes.Makeup));

        var summary = await repository.GetSummaryAsync(created.Value.SessionId);
        summary.Value!.Type.Should().Be(SessionTypes.Makeup);
        summary.Value.Price.Should().BeNull();
        summary.Value.PaymentStatus.Should().BeNull();
        summary.Value.RemainingAmount.Should().Be(0);
    }

    [Fact]
    public async Task Updating_the_payment_marks_the_session_paid()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateSessionRepository(host);

        var updated = await repository.UpdatePaymentAsync(3, true, PaymentStatuses.Paid, 500, 0);

        updated.IsSuccess.Should().BeTrue(updated.Failure?.Detail);
        var summary = await repository.GetSummaryAsync(3);
        summary.Value!.IsPaid.Should().BeTrue();
        summary.Value.PaymentStatus.Should().Be(PaymentStatuses.Paid);
        summary.Value.PaidAmount.Should().Be(500);
        summary.Value.RemainingAmount.Should().Be(0);
    }

    [Fact]
    public async Task Updating_the_payment_of_a_missing_session_reports_not_found()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateSessionRepository(host);

        var result = await repository.UpdatePaymentAsync(999, true, PaymentStatuses.Paid, 10, 0);

        result.IsFailure.Should().BeTrue();
        result.Failure!.Kind.Should().Be(SchoolManagement.Application.Common.FailureKind.NotFound);
    }

    [Fact]
    public async Task The_report_groups_by_student_and_honours_the_period()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateAttendanceRepository(host);

        var oneDay = await repository.GetReportAsync(new AttendanceReportQuery(Day, Day, GroupId: 1));

        oneDay.IsSuccess.Should().BeTrue(oneDay.Failure?.Detail);
        oneDay.Value.Should().HaveCount(2);
        oneDay.Value.Single(row => row.StudentId == 1).PresentCount.Should().Be(1);
        oneDay.Value.Single(row => row.StudentId == 2).AbsentCount.Should().Be(1);

        var range = await repository.GetReportAsync(
            new AttendanceReportQuery(Day.AddDays(-1), Day.AddDays(1), GroupId: 1));
        var alice = range.Value.Single(row => row.StudentId == 1);
        alice.SessionCount.Should().Be(2);
        alice.PresentCount.Should().Be(1);
        alice.AbsentCount.Should().Be(0);
        alice.LateCount.Should().Be(1);

        var single = await repository.GetReportAsync(
            new AttendanceReportQuery(Day.AddDays(-1), Day.AddDays(1), StudentId: 1));
        single.Value.Should().ContainSingle().Which.StudentId.Should().Be(1);
    }

    [Fact]
    public async Task The_report_reads_unexcused_as_an_absence()
    {
        await using var host = await SeededHostAsync();
        await host.ExecuteAsync("UPDATE attendance SET status = 'unexcused' WHERE id = 2;");
        var repository = CreateAttendanceRepository(host);

        var result = await repository.GetReportAsync(new AttendanceReportQuery(Day, Day));

        result.Value.Single(row => row.StudentId == 2).AbsentCount.Should().Be(1);
        result.Value.Single(row => row.StudentId == 2).PresentCount.Should().Be(0);
    }

    [Fact]
    public async Task Guardian_contacts_are_the_active_students_with_a_phone()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateSessionRepository(host);

        var result = await repository.GetGuardianContactsAsync(1);

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.Should().HaveCount(2);
        result.Value.Select(contact => contact.Phone)
            .Should().BeEquivalentTo(new[] { "+213555000001", "+213555000002" });
        result.Value.Select(contact => contact.StudentId).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    // Helpers ---------------------------------------------------------------

    private static SessionRepository CreateSessionRepository(DatabaseTestHost host) => new(
        host.CreateFactory(),
        host.Clock,
        NullLogger<SessionRepository>.Instance);

    private static AttendanceRepository CreateAttendanceRepository(DatabaseTestHost host) => new(
        host.CreateFactory(),
        host.Clock,
        NullLogger<AttendanceRepository>.Instance);

    private static async Task<DatabaseTestHost> SeededHostAsync()
    {
        var host = DatabaseTestHost.Create();
        await host.InitializeAsync();
        await host.ExecuteAsync(SeedScript());
        return host;
    }

    private static string SeedScript()
    {
        var day = Stamp(Day);
        var previous = Stamp(Day.AddDays(-1));
        var next = Stamp(Day.AddDays(1));
        var start9 = Stamp(Day.AddHours(9));
        var end10 = Stamp(Day.AddHours(10));
        var nextStart9 = Stamp(Day.AddDays(1).AddHours(9));
        var nextEnd10 = Stamp(Day.AddDays(1).AddHours(10));
        var start11 = Stamp(Day.AddHours(11));
        var end12 = Stamp(Day.AddHours(12));
        var previousStart = Stamp(Day.AddDays(-1).AddHours(9));
        var previousEnd = Stamp(Day.AddDays(-1).AddHours(10));
        var dueFuture = Stamp(Day.AddDays(5));
        var duePast = Stamp(Day.AddDays(-30));
        var submittedAt = Stamp(Day.AddDays(-29));

        return $"""
            INSERT INTO "academic_levels" ("id", "name", "order_index") VALUES (1, 'Level One', 1);
            INSERT INTO "grades" ("id", "academic_level_id", "name", "order_index") VALUES (1, 1, 'Grade One', 1);
            INSERT INTO "subjects" ("id", "name") VALUES (1, 'Mathematics');
            INSERT INTO "teachers" ("id", "full_name") VALUES (1, 'Teacher One');
            INSERT INTO "groups"
                ("id", "subject_id", "teacher_id", "academic_level_id", "grade_id", "name",
                 "max_students", "monthly_fee", "is_active")
                VALUES
                (1, 1, 1, 1, 1, 'Group Alpha', 30, 1000, 1),
                (2, 1, 1, 1, 1, 'Group Beta', 30, 2000, 1);
            INSERT INTO "guardians" ("id", "full_name", "phone") VALUES
                (1, 'Guardian One', '+213555000001'),
                (2, 'Guardian Two', '+213555000002');
            INSERT INTO "students"
                ("id", "guardian_id", "student_code", "first_name", "last_name", "status")
                VALUES
                (1, 1, 'STU-2026-0001', 'Alice', 'Anderson', 'active'),
                (2, 2, 'STU-2026-0002', 'Bob', 'Brown', 'active'),
                (3, NULL, 'STU-2026-0003', 'Carol', 'Clark', 'active'),
                (4, NULL, 'STU-2026-0004', 'Dave', 'Davis', 'active'),
                (5, NULL, 'STU-2026-0005', 'Eve', 'Evans', 'active');
            INSERT INTO "enrollments" ("id", "student_id", "group_id", "status") VALUES
                (1, 1, 1, 'active'),
                (2, 2, 1, 'active'),
                (3, 3, 1, 'withdrawn'),
                (4, 4, 2, 'active'),
                (5, 5, 1, 'active');
            INSERT INTO "sessions"
                ("id", "group_id", "session_date", "start_time", "end_time", "type", "status",
                 "lesson_topic", "price", "payment_status", "remaining_amount")
                VALUES
                (1, 1, {day}, {start9}, {end10}, 'regular', 'scheduled', 'Algebra', NULL, NULL, 0),
                (2, 1, {previous}, {previousStart}, {previousEnd}, 'regular', 'cancelled', NULL, NULL, NULL, 0),
                (3, 2, {day}, {start11}, {end12}, 'extra', 'scheduled', NULL, 500, 'unpaid', 500),
                (4, 1, {next}, {nextStart9}, {nextEnd10}, 'makeup', 'completed', NULL, NULL, NULL, 0);
            INSERT INTO "attendance" ("id", "session_id", "student_id", "status", "check_in_time") VALUES
                (1, 1, 1, 'present', {start9}),
                (2, 1, 2, 'absent', NULL),
                (3, 4, 1, 'late', {nextStart9});
            INSERT INTO "homeworks"
                ("id", "session_id", "target_session_id", "group_id", "title", "description",
                 "due_date", "is_completed")
                VALUES
                (1, 1, 1, 1, 'Exercise 1', 'Page 10', {dueFuture}, 0),
                (2, 1, 1, 1, 'Exercise 2', 'Page 11', {duePast}, 0),
                (3, 3, 3, 2, 'Exercise 3', 'Page 12', {dueFuture}, 1);
            INSERT INTO "homework_submissions"
                ("id", "homework_id", "student_id", "status", "submitted_at")
                VALUES
                (1, 1, 1, 'submitted', {submittedAt}),
                (2, 1, 2, 'late', {submittedAt});
            """;
    }

    private static long Stamp(DateTime value) => UnixSecondsDateTimeConverter.ToUnixSeconds(value);
}
