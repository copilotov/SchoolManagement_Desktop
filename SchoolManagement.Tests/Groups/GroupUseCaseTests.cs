using FluentAssertions;
using NSubstitute;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Groups;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Students;
using SchoolManagement.Application.Time;
using SchoolManagement.Application.UseCases;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Policies;

namespace SchoolManagement.Tests.Groups;

/// <summary>
/// Phase 6 use cases. Every write must pass the license gate before touching its
/// repository, validation must short-circuit, and the read use cases must join
/// their repositories in one call without swallowing failures.
/// </summary>
public sealed class GroupUseCaseTests
{
    // Write gate ------------------------------------------------------------

    [Fact]
    public async Task Save_group_checks_the_license_gate_first()
    {
        var repository = Substitute.For<IGroupRepository>();
        var guard = Blocking();

        var result = await new SaveGroupUseCase(repository, guard).ExecuteAsync(Request());

        result.Failure!.Kind.Should().Be(FailureKind.License);
        guard.Received(1).EnsureCanWrite();
        await repository.DidNotReceive().SaveAsync(
            Arg.Any<GroupSaveRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_group_runs_the_guard_once_and_normalizes_before_the_repository()
    {
        var repository = Substitute.For<IGroupRepository>();
        GroupSaveRequest? stored = null;
        repository
            .SaveAsync(Arg.Do<GroupSaveRequest>(request => stored = request), Arg.Any<CancellationToken>())
            .Returns(Result<GroupSaveOutcome>.Success(new GroupSaveOutcome(5, true)));
        var guard = Allowing();

        var result = await new SaveGroupUseCase(repository, guard)
            .ExecuteAsync(Request(name: "  Group One  ", levelId: 0));

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.GroupId.Should().Be(5);
        stored.Should().NotBeNull();
        stored!.Name.Should().Be("Group One");
        stored.AcademicLevelId.Should().BeNull();
        guard.Received(1).EnsureCanWrite();
    }

    [Fact]
    public async Task Save_group_validation_short_circuits_before_the_repository()
    {
        var repository = Substitute.For<IGroupRepository>();

        var result = await new SaveGroupUseCase(repository, Allowing())
            .ExecuteAsync(Request(name: "   "));

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Validators.Required);
        await repository.DidNotReceive().SaveAsync(
            Arg.Any<GroupSaveRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Remove_group_checks_the_license_gate_first()
    {
        var repository = Substitute.For<IGroupRepository>();
        var guard = Blocking();

        var result = await new RemoveGroupUseCase(repository, guard).ExecuteAsync(1);

        result.Failure!.Kind.Should().Be(FailureKind.License);
        guard.Received(1).EnsureCanWrite();
        await repository.DidNotReceive().GetDependenciesAsync(
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Remove_group_reports_the_counted_links_without_deleting()
    {
        var repository = Substitute.For<IGroupRepository>();
        repository.GetDependenciesAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<GroupDependencies>.Success(new GroupDependencies(2, 3, 4)));

        var result = await new RemoveGroupUseCase(repository, Allowing()).ExecuteAsync(1);

        result.IsFailure.Should().BeTrue();
        result.Failure!.MessageKey.Should().Be(TranslationKeys.Groups.DeleteBlocked);
        result.Failure.Parameters["enrollments"].Should().Be("2");
        result.Failure.Parameters["sessions"].Should().Be("3");
        result.Failure.Parameters["slots"].Should().Be("4");
        await repository.DidNotReceive().DeleteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Remove_group_deletes_a_free_group()
    {
        var repository = Substitute.For<IGroupRepository>();
        repository.GetDependenciesAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<GroupDependencies>.Success(new GroupDependencies(0, 0, 0)));
        repository.DeleteAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        var result = await new RemoveGroupUseCase(repository, Allowing()).ExecuteAsync(1);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        await repository.Received(1).DeleteAsync(1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Remove_group_propagates_a_dependency_failure()
    {
        var repository = Substitute.For<IGroupRepository>();
        repository.GetDependenciesAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<GroupDependencies>.Fail(Failure.Database()));

        var result = await new RemoveGroupUseCase(repository, Allowing()).ExecuteAsync(1);

        result.Failure!.Kind.Should().Be(FailureKind.Database);
        await repository.DidNotReceive().DeleteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Enroll_checks_the_license_gate_first()
    {
        var repository = Substitute.For<IEnrollmentRepository>();
        var guard = Blocking();

        var result = await new EnrollStudentInGroupUseCase(repository, guard)
            .ExecuteAsync(new EnrollmentSaveRequest(1, 1));

        result.Failure!.Kind.Should().Be(FailureKind.License);
        guard.Received(1).EnsureCanWrite();
        await repository.DidNotReceive().EnrollAsync(
            Arg.Any<EnrollmentSaveRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, -1)]
    public async Task Enroll_rejects_a_missing_group_or_student(int groupId, int studentId)
    {
        var repository = Substitute.For<IEnrollmentRepository>();

        var result = await new EnrollStudentInGroupUseCase(repository, Allowing())
            .ExecuteAsync(new EnrollmentSaveRequest(groupId, studentId));

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Errors.RequiredFields);
        await repository.DidNotReceive().EnrollAsync(
            Arg.Any<EnrollmentSaveRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Enroll_passes_the_request_to_the_repository()
    {
        var repository = Substitute.For<IEnrollmentRepository>();
        repository.EnrollAsync(Arg.Any<EnrollmentSaveRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<EnrollmentSaveOutcome>.Success(new EnrollmentSaveOutcome(7, true, false)));

        var result = await new EnrollStudentInGroupUseCase(repository, Allowing())
            .ExecuteAsync(new EnrollmentSaveRequest(1, 2, "note"));

        result.IsSuccess.Should().BeTrue();
        result.Value.EnrollmentId.Should().Be(7);
        await repository.Received(1).EnrollAsync(
            Arg.Is<EnrollmentSaveRequest>(request =>
                request.GroupId == 1 && request.StudentId == 2 && request.Notes == "note"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Withdraw_checks_the_license_gate_first()
    {
        var repository = Substitute.For<IEnrollmentRepository>();
        var guard = Blocking();

        var result = await new WithdrawEnrollmentUseCase(repository, guard).ExecuteAsync(1);

        result.Failure!.Kind.Should().Be(FailureKind.License);
        guard.Received(1).EnsureCanWrite();
        await repository.DidNotReceive().GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Withdraw_reports_a_missing_enrollment()
    {
        var repository = Substitute.For<IEnrollmentRepository>();
        repository.GetByIdAsync(9, Arg.Any<CancellationToken>())
            .Returns(Result<SchoolManagement.Domain.Entities.Enrollment?>.Success(null));

        var result = await new WithdrawEnrollmentUseCase(repository, Allowing()).ExecuteAsync(9);

        result.Failure!.Kind.Should().Be(FailureKind.NotFound);
        await repository.DidNotReceive().SetStatusAsync(
            Arg.Any<int>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Withdraw_only_changes_the_status_to_withdrawn()
    {
        var repository = Substitute.For<IEnrollmentRepository>();
        repository.GetByIdAsync(9, Arg.Any<CancellationToken>())
            .Returns(Result<SchoolManagement.Domain.Entities.Enrollment?>.Success(
                new SchoolManagement.Domain.Entities.Enrollment { Id = 9 }));
        repository.SetStatusAsync(9, EnrollmentStatuses.Withdrawn, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        var result = await new WithdrawEnrollmentUseCase(repository, Allowing()).ExecuteAsync(9);

        result.IsSuccess.Should().BeTrue();
        await repository.Received(1).SetStatusAsync(
            9,
            EnrollmentStatuses.Withdrawn,
            Arg.Any<CancellationToken>());
        await repository.DidNotReceive().DeleteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_slot_checks_the_license_gate_first()
    {
        var repository = Substitute.For<IScheduleTemplateRepository>();
        var guard = Blocking();

        var result = await new SaveScheduleSlotUseCase(repository, guard).ExecuteAsync(Slot());

        result.Failure!.Kind.Should().Be(FailureKind.License);
        guard.Received(1).EnsureCanWrite();
        await repository.DidNotReceive().SaveSlotAsync(
            Arg.Any<ScheduleSaveRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_slot_validates_and_normalizes_before_the_repository()
    {
        var repository = Substitute.For<IScheduleTemplateRepository>();
        ScheduleSaveRequest? stored = null;
        repository
            .SaveSlotAsync(Arg.Do<ScheduleSaveRequest>(request => stored = request), Arg.Any<CancellationToken>())
            .Returns(Result<ScheduleSaveOutcome>.Success(new ScheduleSaveOutcome(4, true)));

        var result = await new SaveScheduleSlotUseCase(repository, Allowing())
            .ExecuteAsync(Slot(location: "  Room 1  "));

        result.IsSuccess.Should().BeTrue();
        stored!.Location.Should().Be("Room 1");
    }

    [Fact]
    public async Task Save_slot_validation_short_circuits_before_the_repository()
    {
        var repository = Substitute.For<IScheduleTemplateRepository>();

        var result = await new SaveScheduleSlotUseCase(repository, Allowing())
            .ExecuteAsync(Slot(start: new TimeSpan(9, 0, 0), end: new TimeSpan(9, 0, 0)));

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Groups.InvalidTimeRange);
        await repository.DidNotReceive().SaveSlotAsync(
            Arg.Any<ScheduleSaveRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_slot_checks_the_license_gate_first()
    {
        var repository = Substitute.For<IScheduleTemplateRepository>();
        var guard = Blocking();

        var result = await new DeleteScheduleSlotUseCase(repository, guard).ExecuteAsync(1);

        result.Failure!.Kind.Should().Be(FailureKind.License);
        guard.Received(1).EnsureCanWrite();
        await repository.DidNotReceive().DeleteSlotAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_slot_delegates_when_allowed()
    {
        var repository = Substitute.For<IScheduleTemplateRepository>();
        repository.DeleteSlotAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        var result = await new DeleteScheduleSlotUseCase(repository, Allowing()).ExecuteAsync(1);

        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task Generate_sessions_checks_the_license_gate_first()
    {
        var repository = Substitute.For<ISessionRepository>();
        var guard = Blocking();

        var result = await new GenerateSessionsUseCase(repository, guard)
            .ExecuteAsync(new SessionGenerationRequest(new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc)));

        result.Failure!.Kind.Should().Be(FailureKind.License);
        guard.Received(1).EnsureCanWrite();
        await repository.DidNotReceive().GenerateForDateAsync(
            Arg.Any<DateTime>(),
            Arg.Any<int?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Generate_sessions_rejects_a_missing_date()
    {
        var repository = Substitute.For<ISessionRepository>();

        var result = await new GenerateSessionsUseCase(repository, Allowing())
            .ExecuteAsync(new SessionGenerationRequest(default));

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Errors.RequiredFields);
        await repository.DidNotReceive().GenerateForDateAsync(
            Arg.Any<DateTime>(),
            Arg.Any<int?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Generate_sessions_passes_the_optional_group_filter()
    {
        var repository = Substitute.For<ISessionRepository>();
        repository.GenerateForDateAsync(
                Arg.Any<DateTime>(),
                Arg.Any<int?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<SessionGenerationResult>.Success(new SessionGenerationResult(3, 1)));

        var date = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);
        var result = await new GenerateSessionsUseCase(repository, Allowing())
            .ExecuteAsync(new SessionGenerationRequest(date, 5));

        result.IsSuccess.Should().BeTrue();
        result.Value.CreatedCount.Should().Be(3);
        result.Value.AlreadyExisted.Should().Be(1);
        await repository.Received(1).GenerateForDateAsync(date, 5, Arg.Any<CancellationToken>());
    }

    // Read use cases --------------------------------------------------------

    [Fact]
    public async Task Lookups_bundle_the_four_reference_lists()
    {
        var references = Substitute.For<IReferenceDataRepository>();
        references.GetAcademicLevelsAsync(false, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<LookupItem>>.Success(new[] { new LookupItem(1, "Level") }));
        references.GetGradesAsync(null, false, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<LookupItem>>.Success(new[] { new LookupItem(2, "Grade") }));
        references.GetSubjectsAsync(false, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<LookupItem>>.Success(new[] { new LookupItem(3, "Subject") }));
        references.GetTeachersAsync(false, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<LookupItem>>.Success(new[] { new LookupItem(4, "Teacher") }));

        var result = await new GetGroupLookupsUseCase(references).ExecuteAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Levels.Should().ContainSingle().Which.Id.Should().Be(1);
        result.Value.Grades.Should().ContainSingle().Which.Id.Should().Be(2);
        result.Value.Subjects.Should().ContainSingle().Which.Id.Should().Be(3);
        result.Value.Teachers.Should().ContainSingle().Which.Id.Should().Be(4);
    }

    [Fact]
    public async Task Lookups_propagate_a_failure_instead_of_returning_partial_data()
    {
        var references = Substitute.For<IReferenceDataRepository>();
        references.GetAcademicLevelsAsync(false, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<LookupItem>>.Success(Array.Empty<LookupItem>()));
        references.GetGradesAsync(null, false, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<LookupItem>>.Fail(Failure.Database()));

        var result = await new GetGroupLookupsUseCase(references).ExecuteAsync();

        result.Failure!.Kind.Should().Be(FailureKind.Database);
        await references.DidNotReceive().GetSubjectsAsync(
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Group_subjects_are_filtered_by_the_grade_links()
    {
        var grades = Substitute.For<IGradeRepository>();
        grades.GetSubjectIdsAsync(2, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<int>>.Success(new[] { 3, 1 }));

        var references = Substitute.For<IReferenceDataRepository>();
        references.GetSubjectsAsync(false, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<LookupItem>>.Success(new[]
            {
                new LookupItem(1, "Mathematics"),
                new LookupItem(2, "Physics"),
                new LookupItem(3, "Algebra"),
            }));

        var result = await new GetGroupSubjectsUseCase(grades, references).ExecuteAsync(2);

        result.Value.Select(option => option.Name).Should().ContainInOrder("Algebra", "Mathematics");
        result.Value.Select(option => option.Id).Should().BeEquivalentTo(new[] { 3, 1 });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task Group_subjects_return_empty_for_an_invalid_grade(int gradeId)
    {
        var grades = Substitute.For<IGradeRepository>();
        var references = Substitute.For<IReferenceDataRepository>();

        var result = await new GetGroupSubjectsUseCase(grades, references).ExecuteAsync(gradeId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
        await grades.DidNotReceive().GetSubjectIdsAsync(
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Group_subjects_are_empty_when_the_grade_has_no_links()
    {
        var grades = Substitute.For<IGradeRepository>();
        grades.GetSubjectIdsAsync(2, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<int>>.Success(Array.Empty<int>()));
        var references = Substitute.For<IReferenceDataRepository>();

        var result = await new GetGroupSubjectsUseCase(grades, references).ExecuteAsync(2);

        result.Value.Should().BeEmpty();
        await references.DidNotReceive().GetSubjectsAsync(
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Group_subjects_propagate_both_failures()
    {
        var grades = Substitute.For<IGradeRepository>();
        grades.GetSubjectIdsAsync(2, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<int>>.Fail(Failure.Database()));
        var references = Substitute.For<IReferenceDataRepository>();

        (await new GetGroupSubjectsUseCase(grades, references).ExecuteAsync(2))
            .Failure!.Kind.Should().Be(FailureKind.Database);

        grades.GetSubjectIdsAsync(3, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<int>>.Success(new[] { 1 }));
        references.GetSubjectsAsync(false, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<LookupItem>>.Fail(Failure.Database()));

        (await new GetGroupSubjectsUseCase(grades, references).ExecuteAsync(3))
            .Failure!.Kind.Should().Be(FailureKind.Database);
    }

    [Fact]
    public async Task Details_join_the_group_students_slots_and_attendance()
    {
        var groups = Substitute.For<IGroupRepository>();
        groups.GetSummaryAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<GroupSummary?>.Success(Summary()));
        var enrollments = Substitute.For<IEnrollmentRepository>();
        enrollments.GetByGroupAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<EnrollmentListItem>>.Success(new[]
            {
                Enrollment(1, EnrollmentStatuses.Active),
                Enrollment(2, EnrollmentStatuses.Withdrawn),
            }));
        var schedule = Substitute.For<IScheduleTemplateRepository>();
        schedule.GetSlotsAsync(1, null, false, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ScheduleSlotItem>>.Success(Array.Empty<ScheduleSlotItem>()));
        var attendance = Substitute.For<IAttendanceRepository>();
        attendance.GetGroupStatisticsAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<GroupAttendanceStats>.Success(new GroupAttendanceStats(1, 1, 1, 0, 0)));

        var result = await new GetGroupDetailsUseCase(groups, enrollments, schedule, attendance)
            .ExecuteAsync(1);

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.Group.Id.Should().Be(1);
        result.Value.Students.Should().ContainSingle().Which.StudentId.Should().Be(1);
        result.Value.Attendance.PresentCount.Should().Be(1);
    }

    [Fact]
    public async Task Details_report_a_missing_group()
    {
        var groups = Substitute.For<IGroupRepository>();
        groups.GetSummaryAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<GroupSummary?>.Success(null));

        var result = await new GetGroupDetailsUseCase(
                groups,
                Substitute.For<IEnrollmentRepository>(),
                Substitute.For<IScheduleTemplateRepository>(),
                Substitute.For<IAttendanceRepository>())
            .ExecuteAsync(1);

        result.Failure!.Kind.Should().Be(FailureKind.NotFound);
    }

    [Fact]
    public async Task Details_propagate_a_slot_failure()
    {
        var groups = Substitute.For<IGroupRepository>();
        groups.GetSummaryAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<GroupSummary?>.Success(Summary()));
        var enrollments = Substitute.For<IEnrollmentRepository>();
        enrollments.GetByGroupAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<EnrollmentListItem>>.Success(Array.Empty<EnrollmentListItem>()));
        var schedule = Substitute.For<IScheduleTemplateRepository>();
        schedule.GetSlotsAsync(1, null, false, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ScheduleSlotItem>>.Fail(Failure.Database()));

        var result = await new GetGroupDetailsUseCase(
                groups,
                enrollments,
                schedule,
                Substitute.For<IAttendanceRepository>())
            .ExecuteAsync(1);

        result.Failure!.Kind.Should().Be(FailureKind.Database);
    }

    [Fact]
    public async Task Conflicts_are_classified_as_same_group_or_same_teacher()
    {
        var groups = Substitute.For<IGroupRepository>();
        groups.GetSummaryAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<GroupSummary?>.Success(Summary()));
        var schedule = Substitute.For<IScheduleTemplateRepository>();
        schedule.GetConflictsAsync(
                1,
                1,
                0,
                new TimeSpan(8, 0, 0),
                new TimeSpan(9, 0, 0),
                null,
                Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ScheduleSlotItem>>.Success(new[]
            {
                SlotItem(slotId: 4, groupId: 1, groupName: "Group One", teacherId: 10),
                SlotItem(slotId: 5, groupId: 2, groupName: "Group Two", teacherId: 10),
            }));

        var result = await new GetScheduleConflictsUseCase(schedule, groups)
            .ExecuteAsync(1, 0, new TimeSpan(8, 0, 0), new TimeSpan(9, 0, 0));

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.HasConflicts.Should().BeTrue();
        result.Value.Conflicts.Select(item => item.Kind).Should().BeEquivalentTo(new[]
        {
            ScheduleConflictKind.SameGroup,
            ScheduleConflictKind.SameTeacher,
        });
    }

    [Fact]
    public async Task Conflicts_report_a_missing_group()
    {
        var groups = Substitute.For<IGroupRepository>();
        groups.GetSummaryAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<GroupSummary?>.Success(null));

        var result = await new GetScheduleConflictsUseCase(
                Substitute.For<IScheduleTemplateRepository>(),
                groups)
            .ExecuteAsync(1, 0, new TimeSpan(8, 0, 0), new TimeSpan(9, 0, 0));

        result.Failure!.Kind.Should().Be(FailureKind.NotFound);
    }

    [Fact]
    public async Task Timetable_scopes_the_slots_to_the_group_or_the_teacher()
    {
        var schedule = Substitute.For<IScheduleTemplateRepository>();
        schedule.GetSlotsAsync(3, null, false, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ScheduleSlotItem>>.Success(new[] { SlotItem() }));

        var result = await new GetTimetableUseCase(schedule).ExecuteAsync(3, null);

        result.Value.Should().ContainSingle();
        await schedule.Received(1).GetSlotsAsync(3, null, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Enrollment_board_hides_students_that_are_already_active()
    {
        var groups = Substitute.For<IGroupRepository>();
        groups.GetSummaryAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<GroupSummary?>.Success(Summary()));
        var enrollments = Substitute.For<IEnrollmentRepository>();
        enrollments.GetByGroupAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<EnrollmentListItem>>.Success(new[]
            {
                Enrollment(1, EnrollmentStatuses.Active),
            }));
        var students = Substitute.For<IStudentRepository>();
        students.GetListAsync(Arg.Any<StudentListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StudentListItem>>.Success(new[]
            {
                Student(1),
                Student(2),
            }));

        var result = await new GetEnrollmentBoardUseCase(groups, enrollments, students)
            .ExecuteAsync(1, "ali");

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.ActiveCount.Should().Be(1);
        result.Value.Enrolled.Should().ContainSingle();
        result.Value.Available.Should().ContainSingle().Which.Id.Should().Be(2);
        await students.Received(1).GetListAsync(
            Arg.Is<StudentListQuery>(query => query.SearchTerm == "ali" && query.Status == StudentStatuses.Active),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Enrollment_board_reports_a_missing_group()
    {
        var groups = Substitute.For<IGroupRepository>();
        groups.GetSummaryAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<GroupSummary?>.Success(null));

        var result = await new GetEnrollmentBoardUseCase(
                groups,
                Substitute.For<IEnrollmentRepository>(),
                Substitute.For<IStudentRepository>())
            .ExecuteAsync(1);

        result.Failure!.Kind.Should().Be(FailureKind.NotFound);
    }

    [Fact]
    public async Task The_full_report_joins_the_month_and_hands_the_data_to_the_port()
    {
        var groups = Substitute.For<IGroupRepository>();
        groups.GetSummaryAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<GroupSummary?>.Success(Summary()));
        var enrollments = Substitute.For<IEnrollmentRepository>();
        enrollments.GetByGroupAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<EnrollmentListItem>>.Success(new[]
            {
                Enrollment(1, EnrollmentStatuses.Active),
            }));
        var attendance = Substitute.For<IAttendanceRepository>();
        attendance.GetGroupStatisticsAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<GroupAttendanceStats>.Success(new GroupAttendanceStats(1, 1, 1, 0, 0)));
        var payments = Substitute.For<IPaymentRepository>();
        payments.GetMonthlyDuesAsync("2026-11", 1, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<MonthlyDue>>.Success(new[]
            {
                new MonthlyDue(1, "STU-2026-0001", "Alice Anderson", 1, "Group One", "2026-11", 1000, 400, 0, 600),
                new MonthlyDue(2, "STU-2026-0002", "Bob Brown", 1, "Group One", "2026-11", 1000, 0, 0, 1000),
            }));
        var references = Substitute.For<IReferenceDataRepository>();
        references.GetSchoolNameAsync(Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("Test School"));
        var clock = Substitute.For<ITrustedClock>();
        clock.LocalNow.Returns(new DateTime(2026, 11, 4, 10, 0, 0, DateTimeKind.Utc));
        var report = Substitute.For<IGroupReportService>();
        GroupReportData? captured = null;
        report.BuildFullReportAsync(Arg.Do<GroupReportData>(data => captured = data), Arg.Any<CancellationToken>())
            .Returns(Result<byte[]>.Success(new byte[] { 1, 2, 3 }));

        var result = await new BuildGroupFullReportUseCase(
                groups,
                enrollments,
                attendance,
                payments,
                references,
                clock,
                report)
            .ExecuteAsync(1);

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.Should().BeEquivalentTo(new byte[] { 1, 2, 3 });
        captured.Should().NotBeNull();
        captured!.MonthYear.Should().Be("2026-11");
        captured.SchoolName.Should().Be("Test School");
        captured.Students.Should().ContainSingle().Which.Outstanding.Should().Be(600);
        captured.TotalOutstanding.Should().Be(1600);
    }

    [Fact]
    public async Task The_full_report_reports_a_missing_group()
    {
        var groups = Substitute.For<IGroupRepository>();
        groups.GetSummaryAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<GroupSummary?>.Success(null));

        var result = await new BuildGroupFullReportUseCase(
                groups,
                Substitute.For<IEnrollmentRepository>(),
                Substitute.For<IAttendanceRepository>(),
                Substitute.For<IPaymentRepository>(),
                Substitute.For<IReferenceDataRepository>(),
                Substitute.For<ITrustedClock>(),
                Substitute.For<IGroupReportService>())
            .ExecuteAsync(1);

        result.Failure!.Kind.Should().Be(FailureKind.NotFound);
    }

    [Fact]
    public async Task The_full_report_propagates_a_port_failure()
    {
        var groups = Substitute.For<IGroupRepository>();
        groups.GetSummaryAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<GroupSummary?>.Success(Summary()));
        var enrollments = Substitute.For<IEnrollmentRepository>();
        enrollments.GetByGroupAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<EnrollmentListItem>>.Success(Array.Empty<EnrollmentListItem>()));
        var attendance = Substitute.For<IAttendanceRepository>();
        attendance.GetGroupStatisticsAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<GroupAttendanceStats>.Success(new GroupAttendanceStats(0, 0, 0, 0, 0)));
        var payments = Substitute.For<IPaymentRepository>();
        payments.GetMonthlyDuesAsync(Arg.Any<string>(), 1, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<MonthlyDue>>.Success(Array.Empty<MonthlyDue>()));
        var references = Substitute.For<IReferenceDataRepository>();
        references.GetSchoolNameAsync(Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("Test School"));
        var clock = Substitute.For<ITrustedClock>();
        clock.LocalNow.Returns(new DateTime(2026, 11, 4, 10, 0, 0, DateTimeKind.Utc));
        var report = Substitute.For<IGroupReportService>();
        report.BuildFullReportAsync(Arg.Any<GroupReportData>(), Arg.Any<CancellationToken>())
            .Returns(Result<byte[]>.Fail(Failure.Unexpected(TranslationKeys.Errors.CardRender)));

        var result = await new BuildGroupFullReportUseCase(
                groups,
                enrollments,
                attendance,
                payments,
                references,
                clock,
                report)
            .ExecuteAsync(1);

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Errors.CardRender);
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

    private static GroupSaveRequest Request(
        string name = "Group One",
        int? levelId = 1) => new(
        name,
        levelId,
        GradeId: 1,
        SubjectId: 1,
        TeacherId: 1,
        MaxStudents: 30,
        MonthlyFee: 1000,
        IsActive: true);

    private static ScheduleSaveRequest Slot(
        TimeSpan? start = null,
        TimeSpan? end = null,
        string? location = null) => new(
        1,
        0,
        start ?? new TimeSpan(8, 0, 0),
        end ?? new TimeSpan(9, 0, 0),
        location);

    private static GroupSummary Summary() => new(
        1,
        "Group One",
        1,
        "Mathematics",
        1,
        "Teacher One",
        1,
        "Level One",
        1,
        "Grade One",
        30,
        1000,
        true,
        1,
        2);

    private static EnrollmentListItem Enrollment(int studentId, string status) => new(
        studentId,
        studentId,
        $"STU-2026-{studentId:0000}",
        $"Student {studentId}",
        1,
        "Group One",
        new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
        status,
        0);

    private static StudentListItem Student(int id) => new(
        id,
        $"STU-2026-{id:0000}",
        $"First{id}",
        $"Last{id}",
        null,
        StudentStatuses.Active,
        null,
        null);

    private static ScheduleSlotItem SlotItem(
        int slotId = 1,
        int groupId = 1,
        string groupName = "Group One",
        int? teacherId = 10) => new(
        slotId,
        groupId,
        groupName,
        teacherId,
        "Teacher One",
        0,
        new DateTime(2000, 1, 1, 8, 0, 0, DateTimeKind.Utc),
        new DateTime(2000, 1, 1, 9, 0, 0, DateTimeKind.Utc),
        "Room 1",
        true);
}
