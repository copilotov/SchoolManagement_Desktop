using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolManagement.Application.Academic;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Infrastructure.Persistence.Repositories;

namespace SchoolManagement.Tests.Persistence;

/// <summary>
/// Real SQLite coverage for the Phase 4 repositories: deliberate projections,
/// search/sort/include-inactive, linked counts, case-insensitive subject codes
/// and the transactional grade delete. Every query runs against the original
/// 28-table schema created by <see cref="DatabaseTestHost"/>.
/// </summary>
public sealed class AcademicRepositoryTests
{
    private const string SeedScript = """
        INSERT INTO "academic_levels" ("id", "name", "order_index", "is_active")
            VALUES (1, 'Primary', 1, 1), (2, 'Middle', 2, 1), (3, 'Archived', 3, 0);
        INSERT INTO "subjects" ("id", "name", "code", "description", "is_active")
            VALUES (1, 'Mathematics', 'MATH', 'Numbers', 1),
                   (2, 'Science', 'SCI', NULL, 1),
                   (3, 'Sport', 'SPORT', 'Games', 0);
        INSERT INTO "teachers" ("id", "full_name", "phone", "email", "specialization", "is_active")
            VALUES (1, 'Alice Teacher', '0555', 'alice@example.com', 'Math', 1),
                   (2, 'Bob Teacher', NULL, NULL, 'Science', 1),
                   (3, 'Carol Teacher', NULL, NULL, NULL, 0);
        INSERT INTO "grades" ("id", "academic_level_id", "name", "order_index", "is_active")
            VALUES (1, 1, 'Grade One', 1, 1),
                   (2, 1, 'Grade Two', 2, 1),
                   (3, 2, 'Grade Three', 1, 1),
                   (4, 2, 'Old Grade', 2, 0);
        INSERT INTO "grade_subjects" ("id", "grade_id", "subject_id")
            VALUES (1, 1, 1), (2, 1, 2), (3, 2, 1);
        INSERT INTO "groups"
            ("id", "subject_id", "teacher_id", "academic_level_id", "grade_id", "name",
             "max_students", "monthly_fee")
            VALUES (1, 1, 1, 1, 1, 'Math Group', 10, 1000),
                   (2, 2, 2, 1, 2, 'Science Group', 10, 1200);
        """;

    private static async Task<DatabaseTestHost> SeededHostAsync()
    {
        var host = DatabaseTestHost.Create();
        await host.InitializeAsync();
        await host.ExecuteAsync(SeedScript);
        return host;
    }

    private static IAcademicLevelRepository Levels(DatabaseTestHost host) =>
        new AcademicLevelRepository(
            host.CreateFactory(),
            host.Clock,
            NullLogger<AcademicLevelRepository>.Instance);

    private static IGradeRepository Grades(DatabaseTestHost host) =>
        new GradeRepository(
            host.CreateFactory(),
            host.Clock,
            NullLogger<GradeRepository>.Instance);

    private static ISubjectRepository Subjects(DatabaseTestHost host) =>
        new SubjectRepository(
            host.CreateFactory(),
            host.Clock,
            NullLogger<SubjectRepository>.Instance);

    private static ITeacherRepository Teachers(DatabaseTestHost host) =>
        new TeacherRepository(
            host.CreateFactory(),
            host.Clock,
            NullLogger<TeacherRepository>.Instance);

    // Levels ------------------------------------------------------------------

    [Fact]
    public async Task Level_list_hides_inactive_rows_and_counts_links()
    {
        await using var host = await SeededHostAsync();
        var repository = Levels(host);

        var active = await repository.GetListAsync(new AcademicListQuery(null, IncludeInactive: false));

        active.IsSuccess.Should().BeTrue();
        active.Value.Select(level => level.Name).Should().ContainInOrder("Middle", "Primary");

        var primary = active.Value.Single(level => level.Id == 1);
        primary.GradesCount.Should().Be(2);
        primary.GroupsCount.Should().Be(2);

        var middle = active.Value.Single(level => level.Id == 2);
        middle.GradesCount.Should().Be(2);
        middle.GroupsCount.Should().Be(0);

        var all = await repository.GetListAsync(new AcademicListQuery(null, IncludeInactive: true));
        all.Value.Should().HaveCount(3);
    }

    [Fact]
    public async Task Level_list_searches_by_name_and_sorts_by_order()
    {
        await using var host = await SeededHostAsync();
        var repository = Levels(host);

        var search = await repository.GetListAsync(new AcademicListQuery("  Mid  ", IncludeInactive: true));
        search.Value.Should().ContainSingle().Which.Id.Should().Be(2);

        var ordered = await repository.GetListAsync(new AcademicListQuery(
            null,
            IncludeInactive: true,
            SortBy: AcademicSortField.Order,
            Descending: true));

        ordered.Value.Select(level => level.OrderIndex).Should().ContainInOrder(3, 2, 1);
    }

    [Fact]
    public async Task Level_list_sorts_by_linked_grades()
    {
        await using var host = await SeededHostAsync();

        var result = await Levels(host).GetListAsync(new AcademicListQuery(
            null,
            IncludeInactive: true,
            SortBy: AcademicSortField.LinkedItems));

        // Archived has no grades; Primary and Middle both have two, so the name
        // breaks that tie.
        result.Value[0].Name.Should().Be("Archived");
        result.Value.Skip(1).Select(level => level.Name)
            .Should().ContainInOrder("Middle", "Primary");
        result.Value.Should().HaveCount(3);
    }

    [Fact]
    public async Task Level_dependency_counts_match_the_seed()
    {
        await using var host = await SeededHostAsync();
        var repository = Levels(host);

        (await repository.CountGradesAsync(1)).Value.Should().Be(2);
        (await repository.CountGradesAsync(2)).Value.Should().Be(2);
        (await repository.CountGroupsAsync(1)).Value.Should().Be(2);
        (await repository.CountGroupsAsync(2)).Value.Should().Be(0);
    }

    // Grades ------------------------------------------------------------------

    [Fact]
    public async Task Grade_list_joins_the_level_name_and_counts_subjects()
    {
        await using var host = await SeededHostAsync();

        var result = await Grades(host).GetListAsync(new AcademicListQuery(null, IncludeInactive: false));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
        result.Value.Select(grade => grade.Name)
            .Should().ContainInOrder("Grade One", "Grade Three", "Grade Two");

        var first = result.Value.Single(grade => grade.Id == 1);
        first.AcademicLevelName.Should().Be("Primary");
        first.SubjectsCount.Should().Be(2);
        first.GroupsCount.Should().Be(1);

        var third = result.Value.Single(grade => grade.Id == 3);
        third.AcademicLevelName.Should().Be("Middle");
        third.SubjectsCount.Should().Be(0);
        third.GroupsCount.Should().Be(0);
    }

    [Fact]
    public async Task Grade_list_searches_sorts_and_includes_inactive_rows()
    {
        await using var host = await SeededHostAsync();
        var repository = Grades(host);

        var search = await repository.GetListAsync(new AcademicListQuery("Three", IncludeInactive: false));
        search.Value.Should().ContainSingle().Which.Id.Should().Be(3);

        var all = await repository.GetListAsync(new AcademicListQuery(null, IncludeInactive: true));
        all.Value.Should().HaveCount(4);

        var byOrder = await repository.GetListAsync(new AcademicListQuery(
            null,
            IncludeInactive: true,
            SortBy: AcademicSortField.Order));

        byOrder.Value.Select(grade => grade.OrderIndex).Should().BeInAscendingOrder();

        var byLinked = await repository.GetListAsync(new AcademicListQuery(
            null,
            IncludeInactive: true,
            SortBy: AcademicSortField.LinkedItems,
            Descending: true));

        byLinked.Value[0].GroupsCount.Should().Be(1);
        byLinked.Value.Take(2).Select(grade => grade.Id).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public async Task Grade_subject_ids_are_returned_in_order()
    {
        await using var host = await SeededHostAsync();

        var result = await Grades(host).GetSubjectIdsAsync(1);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Equal(1, 2);
    }

    [Fact]
    public async Task Setting_grade_subjects_adds_removes_and_ignores_unknown_ids()
    {
        await using var host = await SeededHostAsync();
        var repository = Grades(host);

        var added = await repository.SetSubjectsAsync(1, new[] { 2, 3 });
        added.IsSuccess.Should().BeTrue();
        (await repository.GetSubjectIdsAsync(1)).Value.Should().Equal(2, 3);

        var withUnknown = await repository.SetSubjectsAsync(1, new[] { 1, 999 });
        withUnknown.IsSuccess.Should().BeTrue();
        (await repository.GetSubjectIdsAsync(1)).Value.Should().Equal(1);

        var cleared = await repository.SetSubjectsAsync(1, Array.Empty<int>());
        cleared.IsSuccess.Should().BeTrue();
        (await repository.GetSubjectIdsAsync(1)).Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Setting_subjects_on_a_missing_grade_reports_not_found()
    {
        await using var host = await SeededHostAsync();

        var result = await Grades(host).SetSubjectsAsync(99, new[] { 1 });

        result.IsFailure.Should().BeTrue();
        result.Failure!.Kind.Should().Be(FailureKind.NotFound);
        result.Failure.MessageKey.Should().Be(TranslationKeys.Errors.NotFound);
    }

    [Fact]
    public async Task Deleting_a_grade_also_removes_its_subject_links()
    {
        await using var host = await SeededHostAsync();
        var repository = Grades(host);

        var result = await repository.DeleteAsync(1);

        result.IsSuccess.Should().BeTrue();

        await using var connection = await host.OpenAsync();
        (await DatabaseTestHost.LongAsync(connection, "SELECT COUNT(*) FROM \"grades\"")).Should().Be(3);
        (await DatabaseTestHost.LongAsync(
            connection,
            "SELECT COUNT(*) FROM \"grade_subjects\" WHERE \"grade_id\" = 1")).Should().Be(0);
        (await DatabaseTestHost.LongAsync(
            connection,
            "SELECT COUNT(*) FROM \"grade_subjects\"")).Should().Be(1);
    }

    [Fact]
    public async Task Deleting_a_missing_grade_reports_not_found()
    {
        await using var host = await SeededHostAsync();

        var result = await Grades(host).DeleteAsync(404);

        result.IsFailure.Should().BeTrue();
        result.Failure!.Kind.Should().Be(FailureKind.NotFound);
    }

    [Fact]
    public async Task Grade_dependency_counts_match_the_seed()
    {
        await using var host = await SeededHostAsync();
        var repository = Grades(host);

        (await repository.CountGroupsAsync(1)).Value.Should().Be(1);
        (await repository.CountGroupsAsync(2)).Value.Should().Be(1);
        (await repository.CountGroupsAsync(3)).Value.Should().Be(0);
    }

    // Subjects ----------------------------------------------------------------

    [Fact]
    public async Task Subject_list_searches_name_code_and_description()
    {
        await using var host = await SeededHostAsync();
        var repository = Subjects(host);

        var active = await repository.GetListAsync(new AcademicListQuery(null, IncludeInactive: false));
        active.Value.Should().HaveCount(2);

        var byName = await repository.GetListAsync(new AcademicListQuery("Math", IncludeInactive: false));
        byName.Value.Should().ContainSingle().Which.Id.Should().Be(1);

        var byCode = await repository.GetListAsync(new AcademicListQuery("SCI", IncludeInactive: false));
        byCode.Value.Should().ContainSingle().Which.Id.Should().Be(2);

        var byDescription = await repository.GetListAsync(
            new AcademicListQuery("Games", IncludeInactive: true));
        byDescription.Value.Should().ContainSingle().Which.Id.Should().Be(3);
    }

    [Fact]
    public async Task Subject_list_sorts_by_code_and_counts_links()
    {
        await using var host = await SeededHostAsync();

        var result = await Subjects(host).GetListAsync(new AcademicListQuery(
            null,
            IncludeInactive: true,
            SortBy: AcademicSortField.Order));

        result.Value.Select(subject => subject.Code).Should().ContainInOrder("MATH", "SCI", "SPORT");

        var math = result.Value.Single(subject => subject.Id == 1);
        math.GradesCount.Should().Be(2);
        math.GroupsCount.Should().Be(1);
        math.Description.Should().Be("Numbers");
    }

    [Fact]
    public async Task Subject_code_uniqueness_is_case_insensitive_and_can_exclude_the_current_id()
    {
        await using var host = await SeededHostAsync();
        var repository = Subjects(host);

        (await repository.CodeExistsAsync("math")).Value.Should().BeTrue();
        (await repository.CodeExistsAsync("  MATH  ")).Value.Should().BeTrue();
        (await repository.CodeExistsAsync("MATH", excludeId: 1)).Value.Should().BeFalse();
        (await repository.CodeExistsAsync("MATH", excludeId: 2)).Value.Should().BeTrue();
        (await repository.CodeExistsAsync("unknown")).Value.Should().BeFalse();
        (await repository.CodeExistsAsync("   ")).Value.Should().BeFalse();
    }

    [Fact]
    public async Task Subject_dependency_counts_match_the_seed()
    {
        await using var host = await SeededHostAsync();
        var repository = Subjects(host);

        (await repository.CountGroupsAsync(1)).Value.Should().Be(1);
        (await repository.CountGradesAsync(1)).Value.Should().Be(2);
        (await repository.CountGradesAsync(3)).Value.Should().Be(0);
    }

    // Teachers ----------------------------------------------------------------

    [Fact]
    public async Task Teacher_list_searches_every_contact_field()
    {
        await using var host = await SeededHostAsync();
        var repository = Teachers(host);

        var active = await repository.GetListAsync(new AcademicListQuery(null, IncludeInactive: false));
        active.Value.Should().HaveCount(2);

        (await repository.GetListAsync(new AcademicListQuery("Alice", IncludeInactive: false)))
            .Value.Should().ContainSingle().Which.Id.Should().Be(1);

        (await repository.GetListAsync(new AcademicListQuery("example.com", IncludeInactive: false)))
            .Value.Should().ContainSingle().Which.Id.Should().Be(1);

        (await repository.GetListAsync(new AcademicListQuery("Science", IncludeInactive: false)))
            .Value.Should().ContainSingle().Which.Id.Should().Be(2);

        var all = await repository.GetListAsync(new AcademicListQuery(null, IncludeInactive: true));
        all.Value.Should().HaveCount(3);
    }

    [Fact]
    public async Task Teacher_list_maps_the_address_and_counts_groups()
    {
        await using var host = await SeededHostAsync();

        var result = await Teachers(host).GetListAsync(new AcademicListQuery(null, IncludeInactive: false));

        var alice = result.Value.Single(teacher => teacher.Id == 1);
        alice.Phone.Should().Be("0555");
        alice.Email.Should().Be("alice@example.com");
        alice.Specialization.Should().Be("Math");
        alice.GroupsCount.Should().Be(1);

        var bob = result.Value.Single(teacher => teacher.Id == 2);
        bob.GroupsCount.Should().Be(1);
        bob.Phone.Should().BeNull();
    }

    [Fact]
    public async Task Teacher_dependency_counts_match_the_seed()
    {
        await using var host = await SeededHostAsync();
        var repository = Teachers(host);

        (await repository.CountGroupsAsync(1)).Value.Should().Be(1);
        (await repository.CountGroupsAsync(2)).Value.Should().Be(1);
        (await repository.CountGroupsAsync(3)).Value.Should().Be(0);
    }

    [Fact]
    public async Task Academic_repositories_surface_a_cancelled_operation_as_an_exception()
    {
        await using var host = await SeededHostAsync();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var act = async () => await Levels(host).GetListAsync(
            new AcademicListQuery(null, IncludeInactive: false),
            cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
