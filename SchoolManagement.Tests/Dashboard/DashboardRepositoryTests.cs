using System.Diagnostics;
using System.Globalization;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolManagement.Application.Dashboard;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Domain.Common;
using SchoolManagement.Infrastructure.Persistence.Converters;
using SchoolManagement.Infrastructure.Persistence.Repositories;
using SchoolManagement.Tests.Persistence;

namespace SchoolManagement.Tests.Dashboard;

/// <summary>
/// Phase D4 - real SQLite coverage of the read-only dashboard aggregates and the
/// single snapshot call. The last test seeds a large school and proves the whole
/// command center loads in under 300 ms.
/// </summary>
public sealed class DashboardRepositoryTests
{
    private static readonly DateTime Today = new(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime WeekStart = new(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);

    private static readonly string[] Months =
    [
        "2026-06", "2026-07", "2026-08", "2026-09", "2026-10", "2026-11",
    ];

    [Fact]
    public async Task Today_sessions_project_the_group_name_and_both_counters()
    {
        await using var host = await SeededHostAsync();

        var result = await CreateDashboard(host).GetTodaySessionsAsync(Today);

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.Should().HaveCount(100);
        result.Value.Should().OnlyContain(session =>
            session.GroupName.StartsWith("Group ", StringComparison.Ordinal)
            && session.StudentCount == 40
            && session.AttendanceCount == 10);
        result.Value.Should().BeInAscendingOrder(session => session.StartTime);
        result.Value[0].CanTakeAttendance.Should().BeTrue();
        result.Value[0].HasAttendance.Should().BeTrue();
    }

    [Fact]
    public async Task Absence_alerts_return_the_students_at_or_over_the_limit()
    {
        await using var host = await SeededHostAsync();

        var result = await CreateDashboard(host).GetAbsenceAlertsAsync(limit: 5, take: 5);

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.Should().HaveCount(5);
        result.Value.Should().OnlyContain(alert => alert.AbsenceCount == 150 && alert.Limit == 5);
        result.Value.Should().BeInDescendingOrder(alert => alert.AbsenceCount);
    }

    [Fact]
    public async Task Absence_alerts_are_empty_for_a_non_positive_limit()
    {
        await using var host = await SeededHostAsync();

        var result = await CreateDashboard(host).GetAbsenceAlertsAsync(limit: 0, take: 5);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Revenue_trend_groups_the_payments_of_every_requested_month()
    {
        await using var host = await SeededHostAsync();

        var result = await CreateDashboard(host).GetRevenueTrendAsync(Months);

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.Should().HaveCount(6);
        result.Value.Should().OnlyContain(point => point.Revenue == 300_000);
        result.Value.Select(point => point.MonthYear).Should().BeEquivalentTo(Months);
    }

    [Fact]
    public async Task Week_attendance_returns_one_point_per_school_day()
    {
        await using var host = await SeededHostAsync();

        var result = await CreateDashboard(host).GetWeekAttendanceAsync(WeekStart);

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.Should().HaveCount(2);
        result.Value.Single(point => point.DayOfWeek == 1).Should().Be(
            new DashboardDayPoint(1, Present: 150, Absent: 250, Late: 100));
        result.Value.Single(point => point.DayOfWeek == 4).Should().Be(
            new DashboardDayPoint(4, Present: 300, Absent: 500, Late: 200));
    }

    [Fact]
    public async Task Level_distribution_counts_each_student_once_per_level()
    {
        await using var host = await SeededHostAsync();

        var result = await CreateDashboard(host).GetLevelDistributionAsync();

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.Should().HaveCount(3);
        result.Value.Sum(slice => slice.StudentCount).Should().Be(1000);
        result.Value.Select(slice => slice.LevelName).Should().BeEquivalentTo(
            "Level One", "Level Two", "Level Three");
    }

    [Fact]
    public async Task The_whole_snapshot_loads_in_under_300ms_on_large_data()
    {
        await using var host = await SeededHostAsync();
        var useCase = CreateUseCase(host);

        // Warm the EF model once so the measurement is the dashboard query, not
        // the one-off model build every process pays on its first query.
        (await useCase.ExecuteAsync()).IsSuccess.Should().BeTrue();

        var stopwatch = Stopwatch.StartNew();
        var result = await useCase.ExecuteAsync();
        stopwatch.Stop();

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        var snapshot = result.Value;
        snapshot.ActiveStudents.Should().Be(500);
        snapshot.ActiveGroups.Should().Be(25);
        snapshot.TodaySessionCount.Should().Be(100);
        snapshot.MonthRevenue.Should().Be(300_000);
        snapshot.MonthOutstanding.Should().Be(700_000);
        snapshot.ArrearsStudentCount.Should().Be(500);
        snapshot.Sessions.Should().HaveCount(100);
        snapshot.RevenueTrend.Should().HaveCount(6);
        snapshot.WeekAttendance.Should().HaveCount(7);
        snapshot.SetupSteps.Should().HaveCount(5);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(300);
    }

    private static async Task<DatabaseTestHost> SeededHostAsync()
    {
        var host = DatabaseTestHost.Create();
        await host.InitializeAsync();
        await host.ExecuteAsync(BuildSeedScript());
        return host;
    }

    private static DashboardRepository CreateDashboard(DatabaseTestHost host)
        => new(host.CreateFactory(), host.Clock, NullLogger<DashboardRepository>.Instance);

    private static GetDashboardSnapshotUseCase CreateUseCase(DatabaseTestHost host)
    {
        var factory = host.CreateFactory();
        var clock = host.Clock;

        // The snapshot reads "today" from the clock. The fixture clock is
        // 31 October while the seed writes its sessions on 4 November, so the
        // dashboard clock is pinned to the seeded day rather than moving every
        // seeded row.
        var dashboardClock = new FixedClock(Today.AddHours(9));

        return new GetDashboardSnapshotUseCase(
            new DashboardRepository(factory, clock, NullLogger<DashboardRepository>.Instance),
            new StudentRepository(factory, clock, NullLogger<StudentRepository>.Instance),
            new GroupRepository(factory, clock, NullLogger<GroupRepository>.Instance),
            new PaymentRepository(factory, clock, NullLogger<PaymentRepository>.Instance),
            new ReferenceDataRepository(factory, clock, NullLogger<ReferenceDataRepository>.Instance),
            new SettingsRepository(factory, clock, NullLogger<SettingsRepository>.Instance),
            dashboardClock);
    }

    /// <summary>
    /// 500 active students, 25 groups, 1000 enrollments, 150 sessions,
    /// 1500 attendance rows and six months of payments (1800 rows).
    /// </summary>
    private static string BuildSeedScript()
    {
        var sql = new StringBuilder(96 * 1024);

        sql.AppendLine("""
            INSERT INTO "academic_levels" ("id", "name", "order_index") VALUES
                (1, 'Level One', 1), (2, 'Level Two', 2), (3, 'Level Three', 3);
            INSERT INTO "grades" ("id", "academic_level_id", "name", "order_index") VALUES
                (1, 1, 'Grade One', 1), (2, 2, 'Grade Two', 2), (3, 3, 'Grade Three', 3);
            INSERT INTO "subjects" ("id", "name") VALUES
                (1, 'Mathematics'), (2, 'Physics'), (3, 'Chemistry');
            INSERT INTO "teachers" ("id", "full_name") VALUES
                (1, 'Teacher One'), (2, 'Teacher Two'), (3, 'Teacher Three');
            """);

        sql.Append("""
            INSERT INTO "groups"
                ("id", "subject_id", "teacher_id", "academic_level_id", "grade_id", "name",
                 "max_students", "monthly_fee", "is_active")
                VALUES
            """);
        for (var group = 1; group <= 25; group++)
        {
            var level = ((group - 1) % 3) + 1;
            sql.Append(CultureInfo.InvariantCulture, $"({group}, {level}, {level}, {level}, {level}, 'Group {group:00}', 40, 1000, 1)");
            sql.Append(group < 25 ? "," : ";");
            sql.AppendLine();
        }

        sql.Append("""
            INSERT INTO "students" ("id", "student_code", "first_name", "last_name", "status")
                VALUES
            """);
        for (var student = 1; student <= 525; student++)
        {
            var status = student <= 500 ? "active" : "inactive";
            sql.Append(CultureInfo.InvariantCulture, $"({student}, 'STU-2026-{student:0000}', 'Student', 'Learner {student:0000}', '{status}')");
            sql.Append(student < 525 ? "," : ";");
            sql.AppendLine();
        }

        sql.Append("""
            INSERT INTO "enrollments" ("id", "student_id", "group_id", "status")
                VALUES
            """);
        var enrollmentId = 0;
        for (var student = 1; student <= 500; student++)
        {
            var first = ((student - 1) % 25) + 1;
            var second = ((student + 10) % 25) + 1;

            enrollmentId++;
            sql.Append(CultureInfo.InvariantCulture, $"({enrollmentId}, {student}, {first}, 'active'),");
            enrollmentId++;
            sql.Append(CultureInfo.InvariantCulture, $"({enrollmentId}, {student}, {second}, 'active')");
            sql.Append(student < 500 ? "," : ";");
            sql.AppendLine();
        }

        sql.Append("""
            INSERT INTO "sessions"
                ("id", "group_id", "session_date", "start_time", "end_time", "type", "status")
                VALUES
            """);
        var sessionId = 0;
        sessionId = AppendSessions(sql, sessionId, Today, count: 100);
        sql.Append(',');
        sql.AppendLine();
        sessionId = AppendSessions(sql, sessionId, Today.AddDays(-3), count: 50);
        sql.AppendLine(";");

        sql.Append("""
            INSERT INTO "attendance" ("id", "session_id", "student_id", "status")
                VALUES
            """);
        var attendanceId = 0;
        for (var session = 1; session <= 150; session++)
        {
            for (var student = 1; student <= 10; student++)
            {
                var status = student <= 5
                    ? AttendanceStatuses.Absent
                    : student <= 8
                        ? AttendanceStatuses.Present
                        : AttendanceStatuses.Late;

                attendanceId++;
                sql.Append(CultureInfo.InvariantCulture, $"({attendanceId}, {session}, {student}, '{status}')");
                sql.Append(session == 150 && student == 10 ? ";" : ",");
                sql.AppendLine();
            }
        }

        sql.Append("""
            INSERT INTO "payments"
                ("id", "student_id", "group_id", "type", "month_year", "amount",
                 "original_amount", "remaining_amount", "payment_date", "method", "receipt_number")
                VALUES
            """);
        var paymentId = 0;
        var paymentDate = UnixSecondsDateTimeConverter.ToUnixSeconds(
            new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc));
        for (var month = 0; month < Months.Length; month++)
        {
            for (var student = 1; student <= 300; student++)
            {
                var group = ((student - 1) % 25) + 1;
                paymentId++;
                sql.Append(CultureInfo.InvariantCulture, $"({paymentId}, {student}, {group}, 'monthly', '{Months[month]}', 1000, 1000, 0, {paymentDate}, 'cash', 'RCP-{paymentId:000000}')");
                sql.Append(month == Months.Length - 1 && student == 300 ? ";" : ",");
                sql.AppendLine();
            }
        }

        return sql.ToString();
    }

    private static int AppendSessions(
        StringBuilder sql,
        int sessionId,
        DateTime date,
        int count)
    {
        for (var index = 0; index < count; index++)
        {
            sessionId++;
            var group = ((sessionId - 1) % 25) + 1;
            var start = date.AddHours(8 + (index % 10));
            var end = start.AddHours(1);
            var sessionDate = UnixSecondsDateTimeConverter.ToUnixSeconds(date);
            var startSeconds = UnixSecondsDateTimeConverter.ToUnixSeconds(start);
            var endSeconds = UnixSecondsDateTimeConverter.ToUnixSeconds(end);

            sql.Append(CultureInfo.InvariantCulture, $"({sessionId}, {group}, {sessionDate}, {startSeconds}, {endSeconds}, 'regular', 'scheduled')");
            if (index < count - 1)
            {
                sql.Append(',');
            }

            sql.AppendLine();
        }

        return sessionId;
    }
}
