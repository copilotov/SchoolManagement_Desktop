using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolManagement.Application.Common;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence.Repositories;

namespace SchoolManagement.Tests.Persistence;

public sealed class RepositoryTests
{
    private const string SeedScript = """
        INSERT INTO "subjects" ("id", "name") VALUES (1, 'Mathematics'), (2, 'Science');
        INSERT INTO "teachers" ("id", "full_name") VALUES (1, 'Teacher One');
        INSERT INTO "academic_levels" ("id", "name", "order_index")
            VALUES (1, 'Level One', 1), (2, 'Level Two', 2);
        INSERT INTO "grades" ("id", "academic_level_id", "name", "order_index")
            VALUES (1, 1, 'Grade One', 1), (2, 1, 'Grade Two', 2), (3, 2, 'Grade Three', 1);
        INSERT INTO "guardians" ("id", "full_name", "phone") VALUES (1, 'Guardian One', '0555');
        INSERT INTO "groups"
            ("id", "subject_id", "teacher_id", "academic_level_id", "grade_id", "name", "max_students", "monthly_fee")
            VALUES (1, 1, 1, 1, 1, 'Math Group', 2, 1000), (2, 2, 1, 1, 2, 'Science Group', 30, 1500);
        INSERT INTO "students" ("id", "guardian_id", "student_code", "first_name", "last_name", "phone", "status")
            VALUES (1, 1, 'S001', 'Alice', 'Anderson', '0101', 'active'),
                   (2, 1, 'S002', 'Bob', 'Brown', '0102', 'active'),
                   (3, NULL, 'S003', 'Carol', 'Clark', NULL, 'inactive');
        INSERT INTO "enrollments" ("id", "student_id", "group_id", "status")
            VALUES (1, 1, 1, 'active'), (2, 2, 1, 'active'), (3, 3, 1, 'inactive'), (4, 1, 2, 'active');
        INSERT INTO "attendance" ("id", "session_id", "student_id", "status")
            VALUES (1, 1, 1, 'absent'), (2, 2, 1, 'unexcused'), (3, 3, 1, 'present'), (4, 4, 2, 'absent');
        INSERT INTO "payments"
            ("id", "student_id", "group_id", "type", "month_year", "amount", "original_amount",
             "remaining_amount", "receipt_number")
            VALUES (1, 1, 1, 'monthly', '2026-10', 400, 1000, 600, 'R001'),
                   (2, 1, 2, 'monthly', '2026-10', 1500, 1500, 0, 'R002'),
                   (3, 2, 1, 'monthly', '2026-09', 300, 1000, 700, 'R003');
        INSERT INTO "exemptions" ("id", "student_id", "type", "amount", "month_year", "reason")
            VALUES (1, 2, 'full', 1000, '2026-10', 'Scholarship');
        INSERT INTO "homeworks" ("id", "session_id", "group_id", "title", "description", "due_date")
            VALUES (1, 1, 1, 'HW1', 'Do it', 1700000000);
        INSERT INTO "homework_submissions" ("id", "homework_id", "student_id", "status")
            VALUES (1, 1, 1, 'submitted'), (2, 1, 2, 'pending');
        """;

    [Fact]
    public async Task Student_search_pages_filters_and_counts()
    {
        await using var host = await SeededHostAsync();
        var repository = new StudentRepository(
            host.CreateFactory(),
            host.Clock,
            NullLogger<StudentRepository>.Instance);

        var active = await repository.SearchAsync(null, true, 0, 10);
        active.IsSuccess.Should().BeTrue(active.Failure?.Detail);
        active.Value.Should().HaveCount(2);
        active.Value.Select(item => item.LastName).Should().ContainInOrder("Anderson", "Brown");
        active.Value[0].GuardianName.Should().Be("Guardian One");

        var page = await repository.SearchAsync("S00", false, 0, 2);
        page.Value.Should().HaveCount(2);

        var inactiveExcluded = await repository.SearchAsync("Carol", true, 0, 10);
        inactiveExcluded.Value.Should().BeEmpty();

        (await repository.CountAsync(true)).Value.Should().Be(2);
        (await repository.CountAsync(false)).Value.Should().Be(3);
    }

    [Fact]
    public async Task Group_summaries_join_reference_data_and_count_active_enrollments()
    {
        await using var host = await SeededHostAsync();
        var repository = new GroupRepository(
            host.CreateFactory(),
            host.Clock,
            NullLogger<GroupRepository>.Instance);

        var result = await repository.GetSummariesAsync(includeInactive: false);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);

        var math = result.Value.Single(group => group.Id == 1);
        math.SubjectName.Should().Be("Mathematics");
        math.TeacherName.Should().Be("Teacher One");
        math.AcademicLevelName.Should().Be("Level One");
        math.GradeName.Should().Be("Grade One");
        math.ActiveEnrollments.Should().Be(2);
        math.IsFull.Should().BeTrue();
        math.RemainingSeats.Should().Be(0);

        var science = result.Value.Single(group => group.Id == 2);
        science.ActiveEnrollments.Should().Be(1);
        science.IsFull.Should().BeFalse();

        (await repository.GetActiveEnrollmentCountAsync(1)).Value.Should().Be(2);
    }

    [Fact]
    public async Task Attendance_counts_absent_and_unexcused_and_applies_the_absence_limit()
    {
        await using var host = await SeededHostAsync();
        var factory = host.CreateFactory();
        var attendance = new AttendanceRepository(
            factory,
            host.Clock,
            NullLogger<AttendanceRepository>.Instance);
        var students = new StudentRepository(
            factory,
            host.Clock,
            NullLogger<StudentRepository>.Instance);

        (await attendance.CountAbsencesAsync(1)).Value.Should().Be(2);
        (await attendance.CountAbsencesAsync(2)).Value.Should().Be(1);

        var summary = await students.GetAbsenceSummaryAsync(1);

        summary.IsSuccess.Should().BeTrue();
        summary.Value.AbsenceCount.Should().Be(2);
        summary.Value.Limit.Should().Be(5);
        summary.Value.HasReachedLimit.Should().BeFalse();
        summary.Value.RemainingBeforeLimit.Should().Be(3);
    }

    [Fact]
    public async Task Monthly_dues_combine_payments_and_exemptions()
    {
        await using var host = await SeededHostAsync();
        var repository = new PaymentRepository(
            host.CreateFactory(),
            host.Clock,
            NullLogger<PaymentRepository>.Instance);

        var result = await repository.GetMonthlyDuesAsync("2026-10");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);

        var aliceMath = result.Value.Single(due => due.StudentId == 1 && due.GroupId == 1);
        aliceMath.StudentCode.Should().Be("S001");
        aliceMath.StudentName.Should().Be("Alice Anderson");
        aliceMath.GroupName.Should().Be("Math Group");
        aliceMath.Expected.Should().Be(1000);
        aliceMath.Paid.Should().Be(400);
        aliceMath.Exempted.Should().Be(0);
        aliceMath.Outstanding.Should().Be(600);

        var bobMath = result.Value.Single(due => due.StudentId == 2 && due.GroupId == 1);
        bobMath.Exempted.Should().Be(1000);
        bobMath.Outstanding.Should().Be(0);

        var aliceScience = result.Value.Single(due => due.StudentId == 1 && due.GroupId == 2);
        aliceScience.Paid.Should().Be(1500);
        aliceScience.Outstanding.Should().Be(0);

        (await repository.GetPaidTotalAsync(1, "2026-10")).Value.Should().Be(1900);

        var single = await repository.GetStudentMonthlyDueAsync(1, 1, "2026-10");
        single.IsSuccess.Should().BeTrue();
        single.Value!.Outstanding.Should().Be(600);
    }

    [Fact]
    public async Task Monthly_dues_reject_an_empty_month()
    {
        await using var host = await SeededHostAsync();
        var repository = new PaymentRepository(
            host.CreateFactory(),
            host.Clock,
            NullLogger<PaymentRepository>.Instance);

        var result = await repository.GetMonthlyDuesAsync("  ");

        result.IsFailure.Should().BeTrue();
        result.Failure!.Kind.Should().Be(FailureKind.Validation);
        result.Failure.MessageKey.Should().Be("errors.requiredFields");
    }

    [Fact]
    public async Task Settings_read_and_update_the_absence_limit_and_fee_settings()
    {
        await using var host = await SeededHostAsync();
        var repository = new SettingsRepository(
            host.CreateFactory(),
            host.Clock,
            NullLogger<SettingsRepository>.Instance);

        (await repository.GetAbsenceLimitAsync()).Value.Should().Be(5);

        (await repository.SetSystemSettingAsync("absence_limit", "3")).IsSuccess.Should().BeTrue();
        (await repository.GetAbsenceLimitAsync()).Value.Should().Be(3);

        var fees = await repository.GetFeeSettingsAsync();
        fees.IsSuccess.Should().BeTrue();
        fees.Value.Id.Should().Be(1);
        fees.Value.RegistrationFeeEnabled.Should().BeFalse();

        var updated = await repository.UpdateFeeSettingsAsync(new FeeSetting
        {
            RegistrationFeeEnabled = true,
            RegistrationFeeAmount = 250,
            Discount3Months = 5,
            Discount6Months = 10,
            Discount12Months = 15,
        });

        updated.IsSuccess.Should().BeTrue();

        var stored = (await repository.GetFeeSettingsAsync()).Value;
        stored.RegistrationFeeEnabled.Should().BeTrue();
        stored.RegistrationFeeAmount.Should().Be(250);
        stored.Discount12Months.Should().Be(15);
    }

    [Fact]
    public async Task Notifications_are_deduplicated_then_marked_read()
    {
        await using var host = await SeededHostAsync();
        var repository = new NotificationRepository(
            host.CreateFactory(),
            host.Clock,
            NullLogger<NotificationRepository>.Instance);

        var added = await repository.AddAsync(new Notification
        {
            Title = "Absence",
            Body = "Student reached the limit",
            Type = NotificationTypes.Attendance,
            RecipientType = RecipientTypes.Student,
            RecipientId = 1,
        });

        added.IsSuccess.Should().BeTrue();

        (await repository.HasUnreadAsync(NotificationTypes.Attendance, 1)).Value.Should().BeTrue();
        (await repository.HasUnreadAsync(NotificationTypes.Attendance, 2)).Value.Should().BeFalse();
        (await repository.CountUnreadAsync()).Value.Should().Be(1);

        var recent = await repository.GetRecentAsync(10);
        recent.Value.Should().ContainSingle(item => item.RecipientId == 1);

        (await repository.MarkReadAsync(added.Value)).IsSuccess.Should().BeTrue();

        (await repository.HasUnreadAsync(NotificationTypes.Attendance, 1)).Value.Should().BeFalse();
        (await repository.CountUnreadAsync()).Value.Should().Be(0);
    }

    [Fact]
    public async Task Homework_lists_submission_counts_and_upserts_a_submission()
    {
        await using var host = await SeededHostAsync();
        var repository = new HomeworkRepository(
            host.CreateFactory(),
            host.Clock,
            NullLogger<HomeworkRepository>.Instance);

        var homeworks = await repository.GetByGroupAsync(1);

        homeworks.IsSuccess.Should().BeTrue();
        homeworks.Value.Should().ContainSingle();
        var homework = homeworks.Value[0];
        homework.SubmissionCount.Should().Be(2);
        homework.SubmittedCount.Should().Be(1);
        homework.PendingCount.Should().Be(1);

        var submissions = await repository.GetSubmissionsAsync(1);
        submissions.Value.Should().HaveCount(2);

        var updated = await repository.SetSubmissionStatusAsync(
            1,
            2,
            HomeworkStatuses.Late,
            new DateTime(2026, 10, 31, 8, 0, 0, DateTimeKind.Utc),
            "Late but received");

        updated.IsSuccess.Should().BeTrue();

        var reRead = await repository.GetSubmissionsAsync(1);
        var bob = reRead.Value.Single(item => item.StudentId == 2);
        bob.Status.Should().Be(HomeworkStatuses.Late);
        bob.Notes.Should().Be("Late but received");
    }

    [Fact]
    public async Task Reference_data_returns_sorted_lookups()
    {
        await using var host = await SeededHostAsync();
        var repository = new ReferenceDataRepository(
            host.CreateFactory(),
            host.Clock,
            NullLogger<ReferenceDataRepository>.Instance);

        (await repository.GetSubjectsAsync(true)).Value.Should().HaveCount(2);
        (await repository.GetTeachersAsync(true)).Value.Should().ContainSingle();
        (await repository.GetGuardiansAsync(true)).Value.Should().ContainSingle();
        (await repository.GetAcademicLevelsAsync(true)).Value.Should().HaveCount(2);

        var gradeOne = await repository.GetGradesAsync(1, true);
        gradeOne.Value.Select(item => item.Name).Should().ContainInOrder("Grade One", "Grade Two");

        (await repository.GetGradesAsync(2, true)).Value.Should().ContainSingle()
            .Which.Name.Should().Be("Grade Three");
    }

    private static async Task<DatabaseTestHost> SeededHostAsync()
    {
        var host = DatabaseTestHost.Create();
        await host.InitializeAsync();
        await host.ExecuteAsync(SeedScript);
        return host;
    }
}
