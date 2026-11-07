using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolManagement.Application.Homeworks;
using SchoolManagement.Domain.Common;
using SchoolManagement.Infrastructure.Persistence.Converters;
using SchoolManagement.Infrastructure.Persistence.Repositories;

namespace SchoolManagement.Tests.Persistence;

/// <summary>
/// Real SQLite coverage of the Phase 7 homework repository: deliberate
/// projections and counts, period/group filters, the submissions board that
/// includes students without a row, and the upsert of one submission status.
/// </summary>
public sealed class HomeworkRepositoryTests
{
    private static readonly DateTime Day = new(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task The_board_lists_students_without_a_row_as_pending_or_missing()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateRepository(host);

        var open = await repository.GetSubmissionBoardAsync(1);

        open.IsSuccess.Should().BeTrue(open.Failure?.Detail);
        open.Value.Should().HaveCount(3);
        open.Value.Single(row => row.StudentId == 1).Status.Should().Be(HomeworkStatuses.Submitted);
        open.Value.Single(row => row.StudentId == 2).Status.Should().Be(HomeworkStatuses.Late);
        open.Value.Single(row => row.StudentId == 5).Status.Should().Be(HomeworkStatuses.Pending);

        var past = await repository.GetSubmissionBoardAsync(2);

        past.Value.Should().HaveCount(3);
        past.Value.Should().OnlyContain(row => row.Status == HomeworkStatuses.Missing);
        past.Value.Should().OnlyContain(row => row.Id == 0);
    }

    [Fact]
    public async Task The_board_reports_a_missing_homework()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateRepository(host);

        var result = await repository.GetSubmissionBoardAsync(999);

        result.IsFailure.Should().BeTrue();
        result.Failure!.Kind.Should().Be(SchoolManagement.Application.Common.FailureKind.NotFound);
    }

    [Fact]
    public async Task Setting_a_submission_creates_then_updates_one_row()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateRepository(host);

        var created = await repository.SetSubmissionStatusAsync(
            1,
            5,
            HomeworkStatuses.Missing,
            null,
            null);
        created.IsSuccess.Should().BeTrue(created.Failure?.Detail);

        await using (var connection = await host.OpenAsync())
        {
            (await DatabaseTestHost.LongAsync(
                    connection,
                    "SELECT COUNT(*) FROM homework_submissions WHERE homework_id = 1 AND student_id = 5"))
                .Should().Be(1);
        }

        var updated = await repository.SetSubmissionStatusAsync(
            1,
            5,
            HomeworkStatuses.Submitted,
            Day,
            "handed in");
        updated.IsSuccess.Should().BeTrue(updated.Failure?.Detail);

        await using (var connection = await host.OpenAsync())
        {
            (await DatabaseTestHost.LongAsync(
                    connection,
                    "SELECT COUNT(*) FROM homework_submissions WHERE homework_id = 1 AND student_id = 5"))
                .Should().Be(1);
        }

        // The repository stores the text it receives; trimming to null is the
        // responsibility of SetHomeworkSubmissionStatusUseCase.
        var board = await repository.GetSubmissionBoardAsync(1);
        var student = board.Value.Single(row => row.StudentId == 5);
        student.Status.Should().Be(HomeworkStatuses.Submitted);
        student.SubmittedAt.Should().Be(Day);
        student.Notes.Should().Be("handed in");
    }

    [Fact]
    public async Task The_homework_list_counts_submissions_and_filters_the_period()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateRepository(host);

        var group = await repository.GetByGroupAsync(1);

        group.IsSuccess.Should().BeTrue(group.Failure?.Detail);
        var open = group.Value.Single(homework => homework.Id == 1);
        open.SubmissionCount.Should().Be(2);
        open.SubmittedCount.Should().Be(2);
        open.PendingCount.Should().Be(0);

        var none = group.Value.Single(homework => homework.Id == 2);
        none.SubmissionCount.Should().Be(0);
        none.SubmittedCount.Should().Be(0);
        none.PendingCount.Should().Be(0);

        var future = await repository.GetListAsync(
            new HomeworkReportQuery(GroupId: 1, FromDate: Day, ToDate: Day.AddDays(10)));
        future.Value.Should().ContainSingle().Which.Id.Should().Be(1);

        var other = await repository.GetListAsync(new HomeworkReportQuery(GroupId: 2));
        other.Value.Should().ContainSingle().Which.Id.Should().Be(3);
        other.Value[0].IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task Due_homeworks_for_a_session_exclude_completed_rows()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateRepository(host);

        var due = await repository.GetDueForSessionAsync(1);
        var other = await repository.GetDueForSessionAsync(3);

        due.Value.Select(homework => homework.Id).Should().ContainInOrder(2, 1);
        other.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task The_summary_reports_a_missing_homework()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateRepository(host);

        (await repository.GetSummaryAsync(1)).Value.Should().NotBeNull();
        (await repository.GetSummaryAsync(999)).Value.Should().BeNull();
    }

    // Helpers ---------------------------------------------------------------

    private static HomeworkRepository CreateRepository(DatabaseTestHost host) => new(
        host.CreateFactory(),
        host.Clock,
        NullLogger<HomeworkRepository>.Instance);

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
        var start9 = Stamp(Day.AddHours(9));
        var end10 = Stamp(Day.AddHours(10));
        var dueFuture = Stamp(Day.AddDays(5));
        var duePast = Stamp(Day.AddDays(-30));
        var submittedAt = Stamp(Day.AddDays(-29));

        return $"""
            INSERT INTO "groups" ("id", "name", "max_students", "monthly_fee", "is_active") VALUES
                (1, 'Group Alpha', 30, 1000, 1),
                (2, 'Group Beta', 30, 2000, 1);
            INSERT INTO "students" ("id", "student_code", "first_name", "last_name", "status") VALUES
                (1, 'STU-2026-0001', 'Alice', 'Anderson', 'active'),
                (2, 'STU-2026-0002', 'Bob', 'Brown', 'active'),
                (3, 'STU-2026-0003', 'Carol', 'Clark', 'active'),
                (5, 'STU-2026-0005', 'Eve', 'Evans', 'active');
            INSERT INTO "enrollments" ("id", "student_id", "group_id", "status") VALUES
                (1, 1, 1, 'active'),
                (2, 2, 1, 'active'),
                (3, 3, 1, 'withdrawn'),
                (4, 5, 1, 'active'),
                (5, 3, 2, 'active');
            INSERT INTO "sessions"
                ("id", "group_id", "session_date", "start_time", "end_time", "type", "status")
                VALUES
                (1, 1, {day}, {start9}, {end10}, 'regular', 'scheduled'),
                (2, 1, {day}, {start9}, {end10}, 'regular', 'scheduled'),
                (3, 2, {day}, {start9}, {end10}, 'regular', 'scheduled');
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
