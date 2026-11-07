using System.Globalization;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Groups;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Students;
using SchoolManagement.Application.Time;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Policies;

namespace SchoolManagement.Application.UseCases;

// Phase 6 read use cases -----------------------------------------------------

/// <summary>Lookup lists for the group form, filter bar and schedule dialog.</summary>
public sealed class GetGroupLookupsUseCase
{
    private readonly IReferenceDataRepository _references;

    public GetGroupLookupsUseCase(IReferenceDataRepository references)
    {
        _references = references ?? throw new ArgumentNullException(nameof(references));
    }

    public async Task<Result<GroupLookups>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var levels = await _references.GetAcademicLevelsAsync(false, cancellationToken).ConfigureAwait(false);
        if (levels.IsFailure)
        {
            return Result<GroupLookups>.Fail(levels.Failure!);
        }

        var grades = await _references.GetGradesAsync(null, false, cancellationToken).ConfigureAwait(false);
        if (grades.IsFailure)
        {
            return Result<GroupLookups>.Fail(grades.Failure!);
        }

        var subjects = await _references.GetSubjectsAsync(false, cancellationToken).ConfigureAwait(false);
        if (subjects.IsFailure)
        {
            return Result<GroupLookups>.Fail(subjects.Failure!);
        }

        var teachers = await _references.GetTeachersAsync(false, cancellationToken).ConfigureAwait(false);
        if (teachers.IsFailure)
        {
            return Result<GroupLookups>.Fail(teachers.Failure!);
        }

        return Result<GroupLookups>.Success(new GroupLookups(
            levels.Value,
            grades.Value,
            subjects.Value,
            teachers.Value));
    }
}

/// <summary>
/// The grade → subject sequence of the group form: only the subjects linked to
/// the selected grade through <c>grade_subjects</c> are offered.
/// </summary>
public sealed class GetGroupSubjectsUseCase
{
    private readonly IGradeRepository _grades;
    private readonly IReferenceDataRepository _references;

    public GetGroupSubjectsUseCase(IGradeRepository grades, IReferenceDataRepository references)
    {
        _grades = grades ?? throw new ArgumentNullException(nameof(grades));
        _references = references ?? throw new ArgumentNullException(nameof(references));
    }

    public async Task<Result<IReadOnlyList<GroupSubjectOption>>> ExecuteAsync(
        int gradeId,
        CancellationToken cancellationToken = default)
    {
        if (gradeId <= 0)
        {
            return Result<IReadOnlyList<GroupSubjectOption>>.Success(Array.Empty<GroupSubjectOption>());
        }

        var linked = await _grades.GetSubjectIdsAsync(gradeId, cancellationToken).ConfigureAwait(false);
        if (linked.IsFailure)
        {
            return Result<IReadOnlyList<GroupSubjectOption>>.Fail(linked.Failure!);
        }

        if (linked.Value.Count == 0)
        {
            return Result<IReadOnlyList<GroupSubjectOption>>.Success(Array.Empty<GroupSubjectOption>());
        }

        var subjects = await _references.GetSubjectsAsync(false, cancellationToken).ConfigureAwait(false);
        if (subjects.IsFailure)
        {
            return Result<IReadOnlyList<GroupSubjectOption>>.Fail(subjects.Failure!);
        }

        var linkedIds = linked.Value.ToHashSet();
        var options = subjects.Value
            .Where(subject => linkedIds.Contains(subject.Id))
            .OrderBy(subject => subject.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(subject => new GroupSubjectOption(subject.Id, subject.Name, null))
            .ToList();

        return Result<IReadOnlyList<GroupSubjectOption>>.Success(options);
    }
}

public sealed class GetGroupsUseCase
{
    private readonly IGroupRepository _repository;

    public GetGroupsUseCase(IGroupRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<Result<IReadOnlyList<GroupSummary>>> ExecuteAsync(
        GroupListQuery query,
        CancellationToken cancellationToken = default)
        => _repository.GetListAsync(query, cancellationToken);
}

public sealed class GetGroupSummaryUseCase
{
    private readonly IGroupRepository _repository;

    public GetGroupSummaryUseCase(IGroupRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<Result<GroupSummary?>> ExecuteAsync(
        int groupId,
        CancellationToken cancellationToken = default)
        => _repository.GetSummaryAsync(groupId, cancellationToken);
}

/// <summary>One call fills the whole group file: header, students, slots and attendance.</summary>
public sealed class GetGroupDetailsUseCase
{
    private readonly IGroupRepository _groups;
    private readonly IEnrollmentRepository _enrollments;
    private readonly IScheduleTemplateRepository _schedule;
    private readonly IAttendanceRepository _attendance;

    public GetGroupDetailsUseCase(
        IGroupRepository groups,
        IEnrollmentRepository enrollments,
        IScheduleTemplateRepository schedule,
        IAttendanceRepository attendance)
    {
        _groups = groups ?? throw new ArgumentNullException(nameof(groups));
        _enrollments = enrollments ?? throw new ArgumentNullException(nameof(enrollments));
        _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
        _attendance = attendance ?? throw new ArgumentNullException(nameof(attendance));
    }

    public async Task<Result<GroupDetailsData>> ExecuteAsync(
        int groupId,
        CancellationToken cancellationToken = default)
    {
        var group = await _groups.GetSummaryAsync(groupId, cancellationToken).ConfigureAwait(false);
        if (group.IsFailure)
        {
            return Result<GroupDetailsData>.Fail(group.Failure!);
        }

        if (group.Value is null)
        {
            return Result<GroupDetailsData>.Fail(
                Failure.NotFound(TranslationKeys.Errors.NotFound));
        }

        var enrollments = await _enrollments.GetByGroupAsync(groupId, cancellationToken).ConfigureAwait(false);
        if (enrollments.IsFailure)
        {
            return Result<GroupDetailsData>.Fail(enrollments.Failure!);
        }

        var slots = await _schedule.GetSlotsAsync(groupId, null, false, cancellationToken).ConfigureAwait(false);
        if (slots.IsFailure)
        {
            return Result<GroupDetailsData>.Fail(slots.Failure!);
        }

        var stats = await _attendance.GetGroupStatisticsAsync(groupId, cancellationToken).ConfigureAwait(false);
        if (stats.IsFailure)
        {
            return Result<GroupDetailsData>.Fail(stats.Failure!);
        }

        var active = enrollments.Value
            .Where(enrollment => string.Equals(
                enrollment.Status,
                EnrollmentStatuses.Active,
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        return Result<GroupDetailsData>.Success(
            new GroupDetailsData(group.Value, active, slots.Value, stats.Value));
    }
}

public sealed class GetGroupScheduleUseCase
{
    private readonly IScheduleTemplateRepository _schedule;

    public GetGroupScheduleUseCase(IScheduleTemplateRepository schedule)
    {
        _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
    }

    public Task<Result<IReadOnlyList<ScheduleSlotItem>>> ExecuteAsync(
        int groupId,
        CancellationToken cancellationToken = default)
        => _schedule.GetSlotsAsync(groupId, null, false, cancellationToken);
}

/// <summary>
/// Overlap check used before a slot is saved. A conflict is reported so the UI
/// can ask for confirmation; the policy never blocks the administrator.
/// </summary>
public sealed class GetScheduleConflictsUseCase
{
    private readonly IScheduleTemplateRepository _schedule;
    private readonly IGroupRepository _groups;

    public GetScheduleConflictsUseCase(
        IScheduleTemplateRepository schedule,
        IGroupRepository groups)
    {
        _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
        _groups = groups ?? throw new ArgumentNullException(nameof(groups));
    }

    public async Task<Result<ScheduleCheckResult>> ExecuteAsync(
        int groupId,
        int dayOfWeek,
        TimeSpan startTime,
        TimeSpan endTime,
        int? excludeSlotId = null,
        CancellationToken cancellationToken = default)
    {
        var group = await _groups.GetSummaryAsync(groupId, cancellationToken).ConfigureAwait(false);
        if (group.IsFailure)
        {
            return Result<ScheduleCheckResult>.Fail(group.Failure!);
        }

        if (group.Value is null)
        {
            return Result<ScheduleCheckResult>.Fail(
                Failure.NotFound(TranslationKeys.Errors.NotFound));
        }

        var conflicts = await _schedule.GetConflictsAsync(
                groupId,
                group.Value.TeacherId,
                dayOfWeek,
                startTime,
                endTime,
                excludeSlotId,
                cancellationToken)
            .ConfigureAwait(false);

        if (conflicts.IsFailure)
        {
            return Result<ScheduleCheckResult>.Fail(conflicts.Failure!);
        }

        var items = conflicts.Value
            .Select(slot => new ScheduleConflictItem(
                slot.GroupId == groupId
                    ? ScheduleConflictKind.SameGroup
                    : ScheduleConflictKind.SameTeacher,
                slot.GroupName,
                slot.DayOfWeek,
                slot.StartTime.TimeOfDay,
                slot.EndTime.TimeOfDay))
            .ToList();

        return Result<ScheduleCheckResult>.Success(new ScheduleCheckResult(items));
    }
}

/// <summary>Weekly slots for the visual timetable, by group or by teacher.</summary>
public sealed class GetTimetableUseCase
{
    private readonly IScheduleTemplateRepository _schedule;

    public GetTimetableUseCase(IScheduleTemplateRepository schedule)
    {
        _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
    }

    public Task<Result<IReadOnlyList<ScheduleSlotItem>>> ExecuteAsync(
        int? groupId,
        int? teacherId,
        CancellationToken cancellationToken = default)
        => _schedule.GetSlotsAsync(groupId, teacherId, false, cancellationToken);
}

/// <summary>Active enrollments plus the active students that are still available.</summary>
public sealed class GetEnrollmentBoardUseCase
{
    private const int AvailableTake = 200;

    private readonly IGroupRepository _groups;
    private readonly IEnrollmentRepository _enrollments;
    private readonly IStudentRepository _students;

    public GetEnrollmentBoardUseCase(
        IGroupRepository groups,
        IEnrollmentRepository enrollments,
        IStudentRepository students)
    {
        _groups = groups ?? throw new ArgumentNullException(nameof(groups));
        _enrollments = enrollments ?? throw new ArgumentNullException(nameof(enrollments));
        _students = students ?? throw new ArgumentNullException(nameof(students));
    }

    public async Task<Result<EnrollmentBoardData>> ExecuteAsync(
        int groupId,
        string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        var group = await _groups.GetSummaryAsync(groupId, cancellationToken).ConfigureAwait(false);
        if (group.IsFailure)
        {
            return Result<EnrollmentBoardData>.Fail(group.Failure!);
        }

        if (group.Value is null)
        {
            return Result<EnrollmentBoardData>.Fail(
                Failure.NotFound(TranslationKeys.Errors.NotFound));
        }

        var enrollments = await _enrollments.GetByGroupAsync(groupId, cancellationToken).ConfigureAwait(false);
        if (enrollments.IsFailure)
        {
            return Result<EnrollmentBoardData>.Fail(enrollments.Failure!);
        }

        var active = enrollments.Value
            .Where(enrollment => string.Equals(
                enrollment.Status,
                EnrollmentStatuses.Active,
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        var activeIds = active.Select(enrollment => enrollment.StudentId).ToHashSet();

        var students = await _students.GetListAsync(
                new StudentListQuery(
                    SearchTerm: searchTerm,
                    Status: StudentStatuses.Active,
                    Take: AvailableTake),
                cancellationToken)
            .ConfigureAwait(false);
        if (students.IsFailure)
        {
            return Result<EnrollmentBoardData>.Fail(students.Failure!);
        }

        var available = students.Value
            .Where(student => !activeIds.Contains(student.Id))
            .ToList();

        return Result<EnrollmentBoardData>.Success(new EnrollmentBoardData(
            group.Value,
            active,
            available,
            active.Count));
    }
}

/// <summary>
/// Joins the group, its students, attendance and the current month's dues, then
/// hands the immutable bundle to the PDF port. Read-only: no license gate.
/// </summary>
public sealed class BuildGroupFullReportUseCase
{
    private readonly IGroupRepository _groups;
    private readonly IEnrollmentRepository _enrollments;
    private readonly IAttendanceRepository _attendance;
    private readonly IPaymentRepository _payments;
    private readonly IReferenceDataRepository _references;
    private readonly ITrustedClock _clock;
    private readonly IGroupReportService _report;

    public BuildGroupFullReportUseCase(
        IGroupRepository groups,
        IEnrollmentRepository enrollments,
        IAttendanceRepository attendance,
        IPaymentRepository payments,
        IReferenceDataRepository references,
        ITrustedClock clock,
        IGroupReportService report)
    {
        _groups = groups ?? throw new ArgumentNullException(nameof(groups));
        _enrollments = enrollments ?? throw new ArgumentNullException(nameof(enrollments));
        _attendance = attendance ?? throw new ArgumentNullException(nameof(attendance));
        _payments = payments ?? throw new ArgumentNullException(nameof(payments));
        _references = references ?? throw new ArgumentNullException(nameof(references));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _report = report ?? throw new ArgumentNullException(nameof(report));
    }

    public async Task<Result<byte[]>> ExecuteAsync(
        int groupId,
        CancellationToken cancellationToken = default)
    {
        var group = await _groups.GetSummaryAsync(groupId, cancellationToken).ConfigureAwait(false);
        if (group.IsFailure)
        {
            return Result<byte[]>.Fail(group.Failure!);
        }

        if (group.Value is null)
        {
            return Result<byte[]>.Fail(Failure.NotFound(TranslationKeys.Errors.NotFound));
        }

        var enrollments = await _enrollments.GetByGroupAsync(groupId, cancellationToken).ConfigureAwait(false);
        if (enrollments.IsFailure)
        {
            return Result<byte[]>.Fail(enrollments.Failure!);
        }

        var attendance = await _attendance.GetGroupStatisticsAsync(groupId, cancellationToken).ConfigureAwait(false);
        if (attendance.IsFailure)
        {
            return Result<byte[]>.Fail(attendance.Failure!);
        }

        var now = _clock.LocalNow;
        var monthYear = now.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var dues = await _payments.GetMonthlyDuesAsync(monthYear, groupId, cancellationToken).ConfigureAwait(false);
        if (dues.IsFailure)
        {
            return Result<byte[]>.Fail(dues.Failure!);
        }

        var school = await _references.GetSchoolNameAsync(cancellationToken).ConfigureAwait(false);
        if (school.IsFailure)
        {
            return Result<byte[]>.Fail(school.Failure!);
        }

        var outstandingByStudent = dues.Value
            .GroupBy(due => due.StudentId)
            .ToDictionary(grouping => grouping.Key, grouping => grouping.Sum(due => due.Outstanding));

        var active = enrollments.Value
            .Where(enrollment => string.Equals(
                enrollment.Status,
                EnrollmentStatuses.Active,
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        var students = active
            .Select(enrollment => new GroupReportStudent(
                enrollment.StudentId,
                enrollment.StudentCode,
                enrollment.StudentName,
                enrollment.Status,
                null,
                outstandingByStudent.TryGetValue(enrollment.StudentId, out var outstanding)
                    ? outstanding
                    : 0))
            .ToList();

        var data = new GroupReportData(
            school.Value,
            null,
            group.Value,
            students,
            attendance.Value,
            dues.Value,
            monthYear,
            now);

        return await _report.BuildFullReportAsync(data, cancellationToken).ConfigureAwait(false);
    }
}

// Phase 6 write use cases ----------------------------------------------------

[WriteOperation]
public sealed class SaveGroupUseCase : WriteUseCase<GroupSaveRequest, GroupSaveOutcome>
{
    private readonly IGroupRepository _repository;

    public SaveGroupUseCase(IGroupRepository repository, ILicenseGuard guard)
        : base(guard)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    protected override Task<Result<GroupSaveOutcome>> ExecuteCoreAsync(
        GroupSaveRequest request,
        CancellationToken cancellationToken)
    {
        var failure = GroupValidator.Validate(request);
        return failure is not null
            ? Task.FromResult(Result<GroupSaveOutcome>.Fail(failure))
            : _repository.SaveAsync(GroupValidator.Normalize(request), cancellationToken);
    }
}

[WriteOperation]
public sealed class RemoveGroupUseCase : WriteUseCase<int, bool>
{
    private readonly IGroupRepository _repository;

    public RemoveGroupUseCase(IGroupRepository repository, ILicenseGuard guard)
        : base(guard)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    protected override async Task<Result<bool>> ExecuteCoreAsync(
        int request,
        CancellationToken cancellationToken)
    {
        var dependencies = await _repository
            .GetDependenciesAsync(request, cancellationToken)
            .ConfigureAwait(false);
        if (dependencies.IsFailure)
        {
            return Result<bool>.Fail(dependencies.Failure!);
        }

        if (dependencies.Value.HasLinks)
        {
            return Result<bool>.Fail(Failure.Validation(
                TranslationKeys.Groups.DeleteBlocked,
                parameters: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["enrollments"] = dependencies.Value.Enrollments.ToString(CultureInfo.InvariantCulture),
                    ["sessions"] = dependencies.Value.Sessions.ToString(CultureInfo.InvariantCulture),
                    ["slots"] = dependencies.Value.ScheduleSlots.ToString(CultureInfo.InvariantCulture),
                }));
        }

        return await _repository.DeleteAsync(request, cancellationToken).ConfigureAwait(false);
    }
}

[WriteOperation]
public sealed class EnrollStudentInGroupUseCase
    : WriteUseCase<EnrollmentSaveRequest, EnrollmentSaveOutcome>
{
    private readonly IEnrollmentRepository _repository;

    public EnrollStudentInGroupUseCase(IEnrollmentRepository repository, ILicenseGuard guard)
        : base(guard)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    protected override Task<Result<EnrollmentSaveOutcome>> ExecuteCoreAsync(
        EnrollmentSaveRequest request,
        CancellationToken cancellationToken)
    {
        if (request.GroupId <= 0 || request.StudentId <= 0)
        {
            return Task.FromResult(Result<EnrollmentSaveOutcome>.Fail(
                Failure.Validation(TranslationKeys.Errors.RequiredFields)));
        }

        return _repository.EnrollAsync(request, cancellationToken);
    }
}

[WriteOperation]
public sealed class WithdrawEnrollmentUseCase : WriteUseCase<int, bool>
{
    private readonly IEnrollmentRepository _repository;

    public WithdrawEnrollmentUseCase(IEnrollmentRepository repository, ILicenseGuard guard)
        : base(guard)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    protected override async Task<Result<bool>> ExecuteCoreAsync(
        int request,
        CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByIdAsync(request, cancellationToken).ConfigureAwait(false);
        if (existing.IsFailure)
        {
            return Result<bool>.Fail(existing.Failure!);
        }

        if (existing.Value is null)
        {
            return Result<bool>.Fail(Failure.NotFound(TranslationKeys.Errors.NotFound));
        }

        return await _repository
            .SetStatusAsync(request, EnrollmentStatuses.Withdrawn, cancellationToken)
            .ConfigureAwait(false);
    }
}

[WriteOperation]
public sealed class SaveScheduleSlotUseCase : WriteUseCase<ScheduleSaveRequest, ScheduleSaveOutcome>
{
    private readonly IScheduleTemplateRepository _repository;

    public SaveScheduleSlotUseCase(IScheduleTemplateRepository repository, ILicenseGuard guard)
        : base(guard)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    protected override Task<Result<ScheduleSaveOutcome>> ExecuteCoreAsync(
        ScheduleSaveRequest request,
        CancellationToken cancellationToken)
    {
        var failure = ScheduleValidator.Validate(request);
        return failure is not null
            ? Task.FromResult(Result<ScheduleSaveOutcome>.Fail(failure))
            : _repository.SaveSlotAsync(ScheduleValidator.Normalize(request), cancellationToken);
    }
}

[WriteOperation]
public sealed class DeleteScheduleSlotUseCase : WriteUseCase<int, bool>
{
    private readonly IScheduleTemplateRepository _repository;

    public DeleteScheduleSlotUseCase(IScheduleTemplateRepository repository, ILicenseGuard guard)
        : base(guard)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    protected override Task<Result<bool>> ExecuteCoreAsync(
        int request,
        CancellationToken cancellationToken)
        => _repository.DeleteSlotAsync(request, cancellationToken);
}

[WriteOperation]
public sealed class GenerateSessionsUseCase
    : WriteUseCase<SessionGenerationRequest, SessionGenerationResult>
{
    private readonly ISessionRepository _repository;

    public GenerateSessionsUseCase(ISessionRepository repository, ILicenseGuard guard)
        : base(guard)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    protected override Task<Result<SessionGenerationResult>> ExecuteCoreAsync(
        SessionGenerationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Date == default)
        {
            return Task.FromResult(Result<SessionGenerationResult>.Fail(
                Failure.Validation(TranslationKeys.Errors.RequiredFields, "date")));
        }

        return _repository.GenerateForDateAsync(request.Date, request.GroupId, cancellationToken);
    }
}
