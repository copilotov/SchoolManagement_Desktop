using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Students;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Policies;
using SchoolManagement.Infrastructure.Persistence.Repositories;

namespace SchoolManagement.Tests.Persistence;

/// <summary>
/// Real SQLite coverage of the Phase 5 student repository: generated codes,
/// guardian transaction, filters/sort/paging, joined card/export rows and the
/// dependency counts used by the delete guard.
/// </summary>
public sealed class StudentRepositoryTests
{
    private const string SeedScript = """
        INSERT INTO "academic_years" ("id", "school_id", "name", "start_date", "end_date", "is_current")
            VALUES (1, 1, '2026-2027', 1767225600, 1798761600, 1);
        INSERT INTO "subjects" ("id", "name") VALUES (1, 'Mathematics');
        INSERT INTO "teachers" ("id", "full_name") VALUES (1, 'Teacher One');
        INSERT INTO "academic_levels" ("id", "name", "order_index") VALUES (1, 'Level One', 1);
        INSERT INTO "grades" ("id", "academic_level_id", "name", "order_index")
            VALUES (1, 1, 'Grade One', 1);
        INSERT INTO "groups"
            ("id", "subject_id", "teacher_id", "academic_level_id", "grade_id", "name", "max_students", "monthly_fee")
            VALUES (1, 1, 1, 1, 1, 'Group One', 20, 1000);
        INSERT INTO "guardians" ("id", "full_name", "phone", "email", "relationship")
            VALUES (1, 'Guardian One', '+213661123456', 'guardian@example.com', 'father');
        INSERT INTO "students"
            ("id", "guardian_id", "student_code", "first_name", "last_name", "phone", "status", "registration_fee")
            VALUES (1, 1, 'STU-2026-0001', 'Alice', 'Anderson', '+213555000001', 'active', 1500),
                   (2, NULL, 'STU-2026-0002', 'Bob', 'Brown', NULL, 'active', 0),
                   (3, NULL, 'CUSTOM-3', 'Carol', 'Clark', NULL, 'inactive', 0);
        INSERT INTO "enrollments" ("id", "student_id", "group_id", "status")
            VALUES (1, 1, 1, 'active'), (2, 2, 1, 'active'), (3, 3, 1, 'inactive');
        INSERT INTO "payments"
            ("id", "student_id", "group_id", "type", "month_year", "amount", "original_amount",
             "remaining_amount", "receipt_number")
            VALUES (1, 1, 1, 'monthly', '2026-10', 500, 1000, 500, 'R001');
        INSERT INTO "attendance" ("id", "session_id", "student_id", "status")
            VALUES (1, 1, 1, 'absent');
        """;

    [Fact]
    public async Task List_filters_searches_sorts_and_pages_in_sql()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateRepository(host);

        var all = await repository.GetListAsync(new StudentListQuery());
        all.IsSuccess.Should().BeTrue(all.Failure?.Detail);
        all.Value.Select(item => item.LastName)
            .Should().ContainInOrder("Anderson", "Brown", "Clark");

        var alice = all.Value[0];
        alice.StudentCode.Should().Be("STU-2026-0001");
        alice.GuardianName.Should().Be("Guardian One");
        alice.LevelName.Should().Be("Level One");
        alice.GradeName.Should().Be("Grade One");
        alice.GroupNames.Should().Be("Group One");
        alice.RegistrationFee.Should().Be(1500);

        var byName = await repository.GetListAsync(new StudentListQuery(SearchTerm: "ander"));
        byName.Value.Should().ContainSingle().Which.FirstName.Should().Be("Alice");

        var byGuardian = await repository.GetListAsync(new StudentListQuery(SearchTerm: "guardian"));
        byGuardian.Value.Should().ContainSingle().Which.Id.Should().Be(1);

        var byLevel = await repository.GetListAsync(new StudentListQuery(AcademicLevelId: 1));
        byLevel.Value.Should().HaveCount(2);

        var byGrade = await repository.GetListAsync(new StudentListQuery(GradeId: 1));
        byGrade.Value.Should().HaveCount(2);

        var byGroup = await repository.GetListAsync(new StudentListQuery(GroupId: 1));
        byGroup.Value.Should().HaveCount(2);

        var inactive = await repository.GetListAsync(new StudentListQuery(Status: StudentStatuses.Inactive));
        inactive.Value.Should().ContainSingle().Which.LastName.Should().Be("Clark");

        var byCode = await repository.GetListAsync(new StudentListQuery(
            SortBy: StudentSortField.Code,
            Descending: true));
        byCode.Value.Select(item => item.LastName)
            .Should().ContainInOrder("Brown", "Anderson", "Clark");

        var page = await repository.GetListAsync(new StudentListQuery(Skip: 1, Take: 1));
        page.Value.Should().ContainSingle().Which.LastName.Should().Be("Brown");

        var count = await repository.CountAsync(new StudentListQuery(Status: StudentStatuses.Active));
        count.Value.Should().Be(2);
    }

    [Fact]
    public async Task Card_data_joins_guardian_placement_school_and_year()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateRepository(host);

        var result = await repository.GetCardDataAsync(new[] { 1, 2, 99 });

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.Should().HaveCount(2);

        var alice = result.Value.Single(card => card.StudentId == 1);
        alice.StudentCode.Should().Be("STU-2026-0001");
        alice.FullName.Should().Be("Alice Anderson");
        alice.LevelName.Should().Be("Level One");
        alice.GradeName.Should().Be("Grade One");
        alice.GroupName.Should().Be("Group One");
        alice.GuardianName.Should().Be("Guardian One");
        alice.GuardianPhone.Should().Be("+213661123456");
        alice.SchoolName.Should().Be("Test School");
        alice.AcademicYear.Should().Be("2026-2027");

        var bob = result.Value.Single(card => card.StudentId == 2);
        bob.GuardianName.Should().BeNull();
        bob.LevelName.Should().Be("Level One");

        (await repository.GetCardDataAsync(Array.Empty<int>())).Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Export_rows_include_the_guardian_and_the_placement()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateRepository(host);

        var result = await repository.GetExportRowsAsync();

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.Should().HaveCount(3);
        result.Value.Select(row => row.LastName)
            .Should().ContainInOrder("Anderson", "Brown", "Clark");

        var alice = result.Value[0];
        alice.GuardianName.Should().Be("Guardian One");
        alice.GuardianEmail.Should().Be("guardian@example.com");
        alice.GuardianRelationship.Should().Be("father");
        alice.LevelName.Should().Be("Level One");
        alice.GradeName.Should().Be("Grade One");
        alice.GroupName.Should().Be("Group One");

        result.Value[1].LevelName.Should().Be("Level One");
        result.Value[2].LevelName.Should().BeNull();
    }

    [Fact]
    public async Task Code_exists_is_case_insensitive_and_excludes_the_edited_row()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateRepository(host);

        (await repository.CodeExistsAsync("stu-2026-0001", null)).Value.Should().BeTrue();
        (await repository.CodeExistsAsync("STU-2026-0001", 1)).Value.Should().BeFalse();
        (await repository.CodeExistsAsync(" STU-2026-0001 ", 2)).Value.Should().BeTrue();
        (await repository.CodeExistsAsync("STU-2026-9999", null)).Value.Should().BeFalse();
        (await repository.CodeExistsAsync("  ", null)).Value.Should().BeFalse();
    }

    [Fact]
    public async Task Save_creates_the_student_and_its_guardian_in_one_transaction()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateRepository(host);

        var first = await repository.SaveAsync(NewStudent("Dina", "Diallo", "Guardian Two"));
        first.IsSuccess.Should().BeTrue(first.Failure?.Detail);
        first.Value.Created.Should().BeTrue();
        first.Value.StudentCode.Should().Be("STU-2026-0003");
        StudentCodeGenerator.ParseSequence(first.Value.StudentCode, 2026).Should().Be(3);

        var second = await repository.SaveAsync(NewStudent("Eli", "Evans", null));
        second.Value.StudentCode.Should().Be("STU-2026-0004");

        var details = await repository.GetDetailsAsync(first.Value.StudentId);
        details.IsSuccess.Should().BeTrue(details.Failure?.Detail);
        details.Value.Should().NotBeNull();
        details.Value!.GuardianName.Should().Be("Guardian Two");
        details.Value.GuardianPhone.Should().Be("+213661000000");
        details.Value.Phone.Should().Be("+213555000009");
        details.Value.FirstName.Should().Be("Dina");

        (await repository.GetListAsync(new StudentListQuery(SearchTerm: "Diallo"))).Value
            .Should().ContainSingle();
    }

    [Fact]
    public async Task Save_updates_an_existing_student_and_keeps_its_code()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateRepository(host);

        var result = await repository.SaveAsync(new StudentSaveRequest(
            "Alicia",
            "Anderson",
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
            null,
            null,
            StudentId: 1));

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.Created.Should().BeFalse();
        result.Value.StudentCode.Should().Be("STU-2026-0001");

        var details = await repository.GetDetailsAsync(1);
        details.Value!.FirstName.Should().Be("Alicia");
        details.Value.StudentCode.Should().Be("STU-2026-0001");
    }

    [Fact]
    public async Task Save_reports_not_found_for_a_missing_student()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateRepository(host);

        var result = await repository.SaveAsync(new StudentSaveRequest(
            "Ghost",
            "Student",
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
            null,
            null,
            StudentId: 4242));

        result.IsFailure.Should().BeTrue();
        result.Failure!.Kind.Should().Be(FailureKind.NotFound);
    }

    [Fact]
    public async Task Concurrent_saves_never_share_a_generated_code()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateRepository(host);

        var tasks = Enumerable.Range(1, 12)
            .Select(index => repository.SaveAsync(NewStudent($"Parallel{index}", "Student", null)))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        results.Should().OnlyContain(result => result.IsSuccess);
        var codes = results.Select(result => result.Value.StudentCode).ToArray();
        codes.Should().OnlyHaveUniqueItems();
        codes.Should().OnlyContain(code => StudentCodeGenerator.IsCodeForYear(code, 2026));
    }

    [Fact]
    public async Task Dependencies_count_enrollments_payments_and_attendance()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateRepository(host);

        var linked = await repository.GetDependenciesAsync(1);
        linked.IsSuccess.Should().BeTrue();
        linked.Value.Enrollments.Should().Be(1);
        linked.Value.Payments.Should().Be(1);
        linked.Value.Attendance.Should().Be(1);
        linked.Value.HasLinks.Should().BeTrue();

        var free = await repository.GetDependenciesAsync(99);
        free.Value.HasLinks.Should().BeFalse();
    }

    [Fact]
    public async Task Details_return_the_joined_guardian_fields()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateRepository(host);

        var details = await repository.GetDetailsAsync(1);

        details.IsSuccess.Should().BeTrue(details.Failure?.Detail);
        details.Value.Should().NotBeNull();
        details.Value!.GuardianId.Should().Be(1);
        details.Value.GuardianName.Should().Be("Guardian One");
        details.Value.GuardianEmail.Should().Be("guardian@example.com");
        details.Value.GuardianRelationship.Should().Be("father");
        details.Value.RegistrationFee.Should().Be(1500);

        var missing = await repository.GetDetailsAsync(99);
        missing.IsSuccess.Should().BeTrue();
        missing.Value.Should().BeNull();
    }

    private static StudentSaveRequest NewStudent(string first, string last, string? guardian) => new(
        first,
        last,
        null,
        null,
        null,
        "+213555000009",
        null,
        StudentStatuses.Active,
        null,
        250,
        guardian,
        guardian is null ? null : "+213661000000",
        null,
        guardian is null ? null : "mother");

    private static StudentRepository CreateRepository(DatabaseTestHost host) => new(
        host.CreateFactory(),
        host.Clock,
        NullLogger<StudentRepository>.Instance);

    private static async Task<DatabaseTestHost> SeededHostAsync()
    {
        var host = DatabaseTestHost.Create();
        await host.InitializeAsync();
        await host.ExecuteAsync(SeedScript);
        return host;
    }
}
