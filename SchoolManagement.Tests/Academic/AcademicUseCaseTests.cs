using FluentAssertions;
using NSubstitute;
using SchoolManagement.Application.Academic;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Application.UseCases;

namespace SchoolManagement.Tests.Academic;

/// <summary>
/// Behaviour of the Phase 4 write use cases: the license gate always runs first,
/// validation short-circuits before the repository, and linked data blocks a
/// delete with a counted, translated failure.
/// </summary>
public sealed class AcademicUseCaseTests
{
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

    private static IEnumerable<(string Name, Func<ILicenseGuard, Task<Failure?>> Run)> BlockedWrites()
    {
        yield return ("add level", async guard =>
        {
            var repository = Substitute.For<IAcademicLevelRepository>();
            var result = await new AddAcademicLevelUseCase(repository, guard)
                .ExecuteAsync(new AcademicLevelInput("Level", 1, true));
            return result.Failure;
        });

        yield return ("update level", async guard =>
        {
            var repository = Substitute.For<IAcademicLevelRepository>();
            var result = await new UpdateAcademicLevelUseCase(repository, guard)
                .ExecuteAsync(new AcademicLevelUpdateRequest(1, new AcademicLevelInput("Level", 1, true)));
            return result.Failure;
        });

        yield return ("delete level", async guard =>
        {
            var repository = Substitute.For<IAcademicLevelRepository>();
            var result = await new DeleteAcademicLevelUseCase(repository, guard).ExecuteAsync(1);
            return result.Failure;
        });

        yield return ("add grade", async guard =>
        {
            var repository = Substitute.For<IGradeRepository>();
            var result = await new AddGradeUseCase(repository, guard)
                .ExecuteAsync(new AcademicGradeInput("Grade", null, 1, true));
            return result.Failure;
        });

        yield return ("update grade", async guard =>
        {
            var repository = Substitute.For<IGradeRepository>();
            var result = await new UpdateGradeUseCase(repository, guard)
                .ExecuteAsync(new AcademicGradeUpdateRequest(1, new AcademicGradeInput("Grade", null, 1, true)));
            return result.Failure;
        });

        yield return ("delete grade", async guard =>
        {
            var repository = Substitute.For<IGradeRepository>();
            var result = await new DeleteGradeUseCase(repository, guard).ExecuteAsync(1);
            return result.Failure;
        });

        yield return ("set grade subjects", async guard =>
        {
            var repository = Substitute.For<IGradeRepository>();
            var result = await new SetGradeSubjectsUseCase(repository, guard)
                .ExecuteAsync(new GradeSubjectAssignment(1, new[] { 2 }));
            return result.Failure;
        });

        yield return ("add subject", async guard =>
        {
            var repository = Substitute.For<ISubjectRepository>();
            var result = await new AddSubjectUseCase(repository, guard)
                .ExecuteAsync(new AcademicSubjectInput("Subject", null, null, true));
            return result.Failure;
        });

        yield return ("update subject", async guard =>
        {
            var repository = Substitute.For<ISubjectRepository>();
            var result = await new UpdateSubjectUseCase(repository, guard)
                .ExecuteAsync(new AcademicSubjectUpdateRequest(
                    1,
                    new AcademicSubjectInput("Subject", null, null, true)));
            return result.Failure;
        });

        yield return ("delete subject", async guard =>
        {
            var repository = Substitute.For<ISubjectRepository>();
            var result = await new DeleteSubjectUseCase(repository, guard).ExecuteAsync(1);
            return result.Failure;
        });

        yield return ("add teacher", async guard =>
        {
            var repository = Substitute.For<ITeacherRepository>();
            var result = await new AddTeacherUseCase(repository, guard)
                .ExecuteAsync(new AcademicTeacherInput("Teacher", null, null, null, null, null, true));
            return result.Failure;
        });

        yield return ("update teacher", async guard =>
        {
            var repository = Substitute.For<ITeacherRepository>();
            var result = await new UpdateTeacherUseCase(repository, guard)
                .ExecuteAsync(new AcademicTeacherUpdateRequest(
                    1,
                    new AcademicTeacherInput("Teacher", null, null, null, null, null, true)));
            return result.Failure;
        });

        yield return ("delete teacher", async guard =>
        {
            var repository = Substitute.For<ITeacherRepository>();
            var result = await new DeleteTeacherUseCase(repository, guard).ExecuteAsync(1);
            return result.Failure;
        });
    }

    [Fact]
    public async Task Every_academic_write_stops_at_the_license_gate()
    {
        foreach (var (name, run) in BlockedWrites())
        {
            var failure = await run(Blocking());

            failure.Should().NotBeNull($"{name} must be gated");
            failure!.Kind.Should().Be(FailureKind.License, $"{name} must be gated");
            failure.MessageKey.Should().Be(TranslationKeys.License.ErrorWriteBlocked);
        }
    }

    [Fact]
    public async Task A_blocked_add_never_touches_the_repository()
    {
        var repository = Substitute.For<IAcademicLevelRepository>();

        await new AddAcademicLevelUseCase(repository, Blocking())
            .ExecuteAsync(new AcademicLevelInput("Level", 1, true));

        await repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task A_blocked_grade_subject_update_never_touches_the_repository()
    {
        var repository = Substitute.For<IGradeRepository>();

        await new SetGradeSubjectsUseCase(repository, Blocking())
            .ExecuteAsync(new GradeSubjectAssignment(1, new[] { 2 }));

        await repository.DidNotReceiveWithAnyArgs().SetSubjectsAsync(default, default!, default);
    }

    [Fact]
    public async Task A_blocked_subject_add_never_checks_the_code()
    {
        var repository = Substitute.For<ISubjectRepository>();

        await new AddSubjectUseCase(repository, Blocking())
            .ExecuteAsync(new AcademicSubjectInput("Subject", "M1", null, true));

        await repository.DidNotReceiveWithAnyArgs().CodeExistsAsync(default!, default, default);
        await repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task A_blocked_teacher_update_never_loads_the_entity()
    {
        var repository = Substitute.For<ITeacherRepository>();

        await new UpdateTeacherUseCase(repository, Blocking())
            .ExecuteAsync(new AcademicTeacherUpdateRequest(
                1,
                new AcademicTeacherInput("Teacher", null, null, null, null, null, true)));

        await repository.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
    }

    [Fact]
    public async Task An_allowed_delete_runs_the_guard_exactly_once()
    {
        var repository = Substitute.For<IAcademicLevelRepository>();
        repository.CountGradesAsync(3, Arg.Any<CancellationToken>()).Returns(Result<int>.Success(0));
        repository.CountGroupsAsync(3, Arg.Any<CancellationToken>()).Returns(Result<int>.Success(0));
        repository.DeleteAsync(3, Arg.Any<CancellationToken>()).Returns(Result<bool>.Success(true));
        var guard = Allowing();

        var result = await new DeleteAcademicLevelUseCase(repository, guard).ExecuteAsync(3);

        result.IsSuccess.Should().BeTrue();
        guard.Received(1).EnsureCanWrite();
        await repository.Received(1).DeleteAsync(3, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_allowed_request_write_runs_the_guard_exactly_once()
    {
        var repository = Substitute.For<ISubjectRepository>();
        repository.AddAsync(Arg.Any<Subject>(), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(9));
        var guard = Allowing();

        var result = await new AddSubjectUseCase(repository, guard)
            .ExecuteAsync(new AcademicSubjectInput("Math", null, null, true));

        result.Value.Should().Be(9);
        guard.Received(1).EnsureCanWrite();
    }

    // Levels ------------------------------------------------------------------

    [Fact]
    public async Task A_level_with_a_blank_name_never_reaches_the_repository()
    {
        var repository = Substitute.For<IAcademicLevelRepository>();

        var result = await new AddAcademicLevelUseCase(repository, Allowing())
            .ExecuteAsync(new AcademicLevelInput("   ", 1, true));

        result.IsFailure.Should().BeTrue();
        result.Failure!.Kind.Should().Be(FailureKind.Validation);
        result.Failure.MessageKey.Should().Be(TranslationKeys.Validators.Required);
        await repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Adding_a_level_trims_the_name()
    {
        AcademicLevel? stored = null;
        var repository = Substitute.For<IAcademicLevelRepository>();
        repository.AddAsync(Arg.Do<AcademicLevel>(entity => stored = entity), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(4));

        var result = await new AddAcademicLevelUseCase(repository, Allowing())
            .ExecuteAsync(new AcademicLevelInput("  Primary  ", 2, false));

        result.Value.Should().Be(4);
        stored.Should().NotBeNull();
        stored!.Name.Should().Be("Primary");
        stored.OrderIndex.Should().Be(2);
        stored.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Updating_a_missing_level_reports_not_found()
    {
        var repository = Substitute.For<IAcademicLevelRepository>();
        repository.GetByIdAsync(7, Arg.Any<CancellationToken>())
            .Returns(Result<AcademicLevel?>.Success(null));

        var result = await new UpdateAcademicLevelUseCase(repository, Allowing())
            .ExecuteAsync(new AcademicLevelUpdateRequest(7, new AcademicLevelInput("Level", 1, true)));

        result.IsFailure.Should().BeTrue();
        result.Failure!.Kind.Should().Be(FailureKind.NotFound);
        result.Failure.MessageKey.Should().Be(TranslationKeys.Errors.NotFound);
        await repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task Updating_a_level_applies_the_new_values()
    {
        var entity = new AcademicLevel { Id = 5, Name = "Old", OrderIndex = 1, IsActive = true };
        var repository = Substitute.For<IAcademicLevelRepository>();
        repository.GetByIdAsync(5, Arg.Any<CancellationToken>())
            .Returns(Result<AcademicLevel?>.Success(entity));
        repository.UpdateAsync(Arg.Any<AcademicLevel>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        var result = await new UpdateAcademicLevelUseCase(repository, Allowing())
            .ExecuteAsync(new AcademicLevelUpdateRequest(5, new AcademicLevelInput(" New ", 9, false)));

        result.IsSuccess.Should().BeTrue();
        entity.Name.Should().Be("New");
        entity.OrderIndex.Should().Be(9);
        entity.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Deleting_a_level_with_grades_is_blocked_with_the_count()
    {
        var repository = Substitute.For<IAcademicLevelRepository>();
        repository.CountGradesAsync(1, Arg.Any<CancellationToken>()).Returns(Result<int>.Success(3));

        var result = await new DeleteAcademicLevelUseCase(repository, Allowing()).ExecuteAsync(1);

        result.IsFailure.Should().BeTrue();
        result.Failure!.Kind.Should().Be(FailureKind.Validation);
        result.Failure.MessageKey.Should().Be(TranslationKeys.Levels.DeleteBlockedGrades);
        result.Failure.Parameters["count"].Should().Be("3");
        await repository.DidNotReceiveWithAnyArgs().DeleteAsync(default, default);
    }

    [Fact]
    public async Task Deleting_a_level_with_groups_is_blocked_with_the_count()
    {
        var repository = Substitute.For<IAcademicLevelRepository>();
        repository.CountGradesAsync(1, Arg.Any<CancellationToken>()).Returns(Result<int>.Success(0));
        repository.CountGroupsAsync(1, Arg.Any<CancellationToken>()).Returns(Result<int>.Success(2));

        var result = await new DeleteAcademicLevelUseCase(repository, Allowing()).ExecuteAsync(1);

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Levels.DeleteBlockedGroups);
        result.Failure.Parameters["count"].Should().Be("2");
        await repository.DidNotReceiveWithAnyArgs().DeleteAsync(default, default);
    }

    [Fact]
    public async Task Deleting_an_unlinked_level_reaches_the_repository()
    {
        var repository = Substitute.For<IAcademicLevelRepository>();
        repository.CountGradesAsync(1, Arg.Any<CancellationToken>()).Returns(Result<int>.Success(0));
        repository.CountGroupsAsync(1, Arg.Any<CancellationToken>()).Returns(Result<int>.Success(0));
        repository.DeleteAsync(1, Arg.Any<CancellationToken>()).Returns(Result<bool>.Success(true));

        var result = await new DeleteAcademicLevelUseCase(repository, Allowing()).ExecuteAsync(1);

        result.IsSuccess.Should().BeTrue();
        await repository.Received(1).DeleteAsync(1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_failed_count_short_circuits_the_level_delete()
    {
        var repository = Substitute.For<IAcademicLevelRepository>();
        repository.CountGradesAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<int>.Fail(Failure.Database()));

        var result = await new DeleteAcademicLevelUseCase(repository, Allowing()).ExecuteAsync(1);

        result.Failure!.Kind.Should().Be(FailureKind.Database);
        await repository.DidNotReceiveWithAnyArgs().CountGroupsAsync(default, default);
    }

    // Grades ------------------------------------------------------------------

    [Fact]
    public async Task Adding_a_grade_validates_the_order_and_maps_the_level()
    {
        var repository = Substitute.For<IGradeRepository>();
        var invalid = await new AddGradeUseCase(repository, Allowing())
            .ExecuteAsync(new AcademicGradeInput("Grade", null, -1, true));

        invalid.Failure!.MessageKey.Should().Be(TranslationKeys.Validators.OrderInvalid);

        Grade? stored = null;
        repository.AddAsync(Arg.Do<Grade>(entity => stored = entity), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(11));

        var result = await new AddGradeUseCase(repository, Allowing())
            .ExecuteAsync(new AcademicGradeInput(" Grade 1 ", 3, 1, true));

        result.Value.Should().Be(11);
        stored!.Name.Should().Be("Grade 1");
        stored.AcademicLevelId.Should().Be(3);
    }

    [Fact]
    public async Task Updating_a_missing_grade_reports_not_found()
    {
        var repository = Substitute.For<IGradeRepository>();
        repository.GetByIdAsync(2, Arg.Any<CancellationToken>())
            .Returns(Result<Grade?>.Success(null));

        var result = await new UpdateGradeUseCase(repository, Allowing())
            .ExecuteAsync(new AcademicGradeUpdateRequest(
                2,
                new AcademicGradeInput("Grade", 1, 1, true)));

        result.Failure!.Kind.Should().Be(FailureKind.NotFound);
    }

    [Fact]
    public async Task Deleting_a_grade_with_groups_is_blocked()
    {
        var repository = Substitute.For<IGradeRepository>();
        repository.CountGroupsAsync(4, Arg.Any<CancellationToken>()).Returns(Result<int>.Success(6));

        var result = await new DeleteGradeUseCase(repository, Allowing()).ExecuteAsync(4);

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Grades.DeleteBlockedGroups);
        result.Failure.Parameters["count"].Should().Be("6");
        await repository.DidNotReceiveWithAnyArgs().DeleteAsync(default, default);
    }

    [Fact]
    public async Task Deleting_an_unlinked_grade_reaches_the_repository()
    {
        var repository = Substitute.For<IGradeRepository>();
        repository.CountGroupsAsync(4, Arg.Any<CancellationToken>()).Returns(Result<int>.Success(0));
        repository.DeleteAsync(4, Arg.Any<CancellationToken>()).Returns(Result<bool>.Success(true));

        var result = await new DeleteGradeUseCase(repository, Allowing()).ExecuteAsync(4);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Setting_grade_subjects_drops_blank_ids_and_duplicates()
    {
        var repository = Substitute.For<IGradeRepository>();
        repository.SetSubjectsAsync(
                Arg.Any<int>(),
                Arg.Any<IReadOnlyCollection<int>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        var result = await new SetGradeSubjectsUseCase(repository, Allowing())
            .ExecuteAsync(new GradeSubjectAssignment(8, new[] { 2, 2, 0, -1, 5 }));

        result.IsSuccess.Should().BeTrue();
        await repository.Received(1).SetSubjectsAsync(
            8,
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 2, 5 })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Setting_grade_subjects_propagates_a_repository_failure()
    {
        var repository = Substitute.For<IGradeRepository>();
        repository.SetSubjectsAsync(
                Arg.Any<int>(),
                Arg.Any<IReadOnlyCollection<int>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Fail(Failure.Database(TranslationKeys.Errors.SaveGradeSubjects)));

        var result = await new SetGradeSubjectsUseCase(repository, Allowing())
            .ExecuteAsync(new GradeSubjectAssignment(8, Array.Empty<int>()));

        result.IsFailure.Should().BeTrue();
        result.Failure!.MessageKey.Should().Be(TranslationKeys.Errors.SaveGradeSubjects);
    }

    // Subjects ----------------------------------------------------------------

    [Fact]
    public async Task A_duplicate_subject_code_is_rejected_with_the_code_field()
    {
        var repository = Substitute.For<ISubjectRepository>();
        repository.CodeExistsAsync("M1", null, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        var result = await new AddSubjectUseCase(repository, Allowing())
            .ExecuteAsync(new AcademicSubjectInput("Math", "  M1  ", null, true));

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Validators.CodeExists);
        result.Failure.Parameters["field"].Should().Be("code");
        await repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task A_subject_without_a_code_skips_the_uniqueness_check()
    {
        var repository = Substitute.For<ISubjectRepository>();
        repository.AddAsync(Arg.Any<Subject>(), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(2));

        var result = await new AddSubjectUseCase(repository, Allowing())
            .ExecuteAsync(new AcademicSubjectInput("Sport", "   ", null, true));

        result.IsSuccess.Should().BeTrue();
        await repository.DidNotReceiveWithAnyArgs().CodeExistsAsync(default!, default, default);
    }

    [Fact]
    public async Task Updating_a_subject_checks_the_code_and_excludes_its_own_id()
    {
        var entity = new Subject { Id = 5, Name = "Old", Code = "OLD", IsActive = true };
        var repository = Substitute.For<ISubjectRepository>();
        repository.CodeExistsAsync("NEW", 5, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));
        repository.GetByIdAsync(5, Arg.Any<CancellationToken>())
            .Returns(Result<Subject?>.Success(entity));
        repository.UpdateAsync(Arg.Any<Subject>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        var result = await new UpdateSubjectUseCase(repository, Allowing())
            .ExecuteAsync(new AcademicSubjectUpdateRequest(
                5,
                new AcademicSubjectInput(" New ", "NEW", "   ", false)));

        result.IsSuccess.Should().BeTrue();
        entity.Name.Should().Be("New");
        entity.Code.Should().Be("NEW");
        entity.Description.Should().BeNull();
        entity.IsActive.Should().BeFalse();
        await repository.Received(1).CodeExistsAsync("NEW", 5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deleting_a_subject_with_groups_is_blocked_before_checking_grades()
    {
        var repository = Substitute.For<ISubjectRepository>();
        repository.CountGroupsAsync(3, Arg.Any<CancellationToken>()).Returns(Result<int>.Success(1));

        var result = await new DeleteSubjectUseCase(repository, Allowing()).ExecuteAsync(3);

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Subjects.DeleteBlockedGroups);
        await repository.DidNotReceiveWithAnyArgs().CountGradesAsync(default, default);
    }

    [Fact]
    public async Task Deleting_a_subject_used_by_grades_is_blocked()
    {
        var repository = Substitute.For<ISubjectRepository>();
        repository.CountGroupsAsync(3, Arg.Any<CancellationToken>()).Returns(Result<int>.Success(0));
        repository.CountGradesAsync(3, Arg.Any<CancellationToken>()).Returns(Result<int>.Success(4));

        var result = await new DeleteSubjectUseCase(repository, Allowing()).ExecuteAsync(3);

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Subjects.DeleteBlockedGrades);
        result.Failure.Parameters["count"].Should().Be("4");
    }

    // Teachers ----------------------------------------------------------------

    [Fact]
    public async Task Adding_a_teacher_with_an_invalid_email_is_rejected()
    {
        var repository = Substitute.For<ITeacherRepository>();

        var result = await new AddTeacherUseCase(repository, Allowing())
            .ExecuteAsync(new AcademicTeacherInput("Teacher", null, "bad", null, null, null, true));

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Validators.InvalidEmail);
        await repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Updating_a_teacher_normalizes_the_optional_fields()
    {
        var entity = new Teacher { Id = 2, FullName = "Old", Phone = "1", IsActive = true };
        var repository = Substitute.For<ITeacherRepository>();
        repository.GetByIdAsync(2, Arg.Any<CancellationToken>())
            .Returns(Result<Teacher?>.Success(entity));
        repository.UpdateAsync(Arg.Any<Teacher>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        var result = await new UpdateTeacherUseCase(repository, Allowing())
            .ExecuteAsync(new AcademicTeacherUpdateRequest(
                2,
                new AcademicTeacherInput(
                    " New Name ",
                    "  ",
                    " t@example.com ",
                    null,
                    " Math ",
                    new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    false)));

        result.IsSuccess.Should().BeTrue();
        entity.FullName.Should().Be("New Name");
        entity.Phone.Should().BeNull();
        entity.Email.Should().Be("t@example.com");
        entity.Address.Should().BeNull();
        entity.Specialization.Should().Be("Math");
        entity.HireDate.Should().Be(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        entity.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Deleting_a_teacher_with_groups_is_blocked()
    {
        var repository = Substitute.For<ITeacherRepository>();
        repository.CountGroupsAsync(9, Arg.Any<CancellationToken>()).Returns(Result<int>.Success(2));

        var result = await new DeleteTeacherUseCase(repository, Allowing()).ExecuteAsync(9);

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Teachers.DeleteBlockedGroups);
        result.Failure.Parameters["count"].Should().Be("2");
        await repository.DidNotReceiveWithAnyArgs().DeleteAsync(default, default);
    }

    [Fact]
    public async Task Deleting_an_unlinked_teacher_reaches_the_repository()
    {
        var repository = Substitute.For<ITeacherRepository>();
        repository.CountGroupsAsync(9, Arg.Any<CancellationToken>()).Returns(Result<int>.Success(0));
        repository.DeleteAsync(9, Arg.Any<CancellationToken>()).Returns(Result<bool>.Success(true));

        var result = await new DeleteTeacherUseCase(repository, Allowing()).ExecuteAsync(9);

        result.IsSuccess.Should().BeTrue();
        await repository.Received(1).DeleteAsync(9, Arg.Any<CancellationToken>());
    }
}
