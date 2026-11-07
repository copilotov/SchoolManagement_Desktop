using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Groups;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Time;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

public sealed class GroupRepository : RepositoryBase, IGroupRepository
{
    public GroupRepository(
        IDbContextFactory<SchoolManagementDbContext> contextFactory,
        ITrustedClock clock,
        ILogger<GroupRepository> logger)
        : base(contextFactory, clock, logger)
    {
    }

    public Task<Result<Group?>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => RunAsync<Group?>(
            "Groups.GetById",
            async context => await context.Groups
                .AsNoTracking()
                .FirstOrDefaultAsync(group => group.Id == id, cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    public Task<Result<int>> AddAsync(Group entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return RunAsync(
            "Groups.Add",
            async context =>
            {
                RepositoryHelper.ApplyCreatedTimestamps(entity, Clock.LocalNow);
                context.Groups.Add(entity);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return entity.Id;
            },
            cancellationToken,
            TranslationKeys.Errors.SaveError);
    }

    public Task<Result<bool>> UpdateAsync(Group entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return RunResultAsync(
            "Groups.Update",
            async context =>
            {
                RepositoryHelper.ApplyUpdatedTimestamp(entity, Clock.LocalNow);
                context.Entry(entity).State = EntityState.Modified;
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return Result<bool>.Success(true);
            },
            cancellationToken,
            TranslationKeys.Errors.SaveError);
    }

    public Task<Result<bool>> DeleteAsync(int id, CancellationToken cancellationToken = default)
        => RunResultAsync(
            "Groups.Delete",
            async context =>
            {
                var group = await context.Groups
                    .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                    .ConfigureAwait(false);

                if (group is null)
                {
                    return Result<bool>.Fail(Failure.NotFound(TranslationKeys.Errors.NotFound));
                }

                context.Groups.Remove(group);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return Result<bool>.Success(true);
            },
            cancellationToken,
            TranslationKeys.Errors.SaveError);

    public Task<Result<IReadOnlyList<GroupSummary>>> GetSummariesAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default)
        => GetListAsync(new GroupListQuery(IncludeInactive: includeInactive), cancellationToken);

    public Task<Result<IReadOnlyList<GroupSummary>>> GetListAsync(
        GroupListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return RunAsync<IReadOnlyList<GroupSummary>>(
            "Groups.GetList",
            async context =>
            {
                var groups = context.Groups.AsNoTracking();
                if (!query.IncludeInactive)
                {
                    groups = groups.Where(group => group.IsActive);
                }

                var term = query.SearchTerm?.Trim();
                if (!string.IsNullOrEmpty(term))
                {
                    // LIKE is case-insensitive for ASCII in SQLite, unlike
                    // string.Contains which translates to a case-sensitive
                    // instr(). The instant search must ignore case.
                    var pattern = $"%{term}%";
                    groups = groups.Where(group =>
                        EF.Functions.Like(group.Name, pattern)
                        || context.Subjects.Any(subject =>
                            subject.Id == group.SubjectId && EF.Functions.Like(subject.Name, pattern))
                        || context.Teachers.Any(teacher =>
                            teacher.Id == group.TeacherId && EF.Functions.Like(teacher.FullName, pattern))
                        || context.Grades.Any(grade =>
                            grade.Id == group.GradeId && EF.Functions.Like(grade.Name, pattern))
                        || context.AcademicLevels.Any(level =>
                            level.Id == group.AcademicLevelId && EF.Functions.Like(level.Name, pattern)));
                }

                groups = ApplySort(context, groups, query.SortBy, query.Descending);

                return await groups
                    .Select(group => new GroupSummary(
                        group.Id,
                        group.Name,
                        group.SubjectId,
                        context.Subjects
                            .Where(subject => subject.Id == group.SubjectId)
                            .Select(subject => subject.Name)
                            .FirstOrDefault(),
                        group.TeacherId,
                        context.Teachers
                            .Where(teacher => teacher.Id == group.TeacherId)
                            .Select(teacher => teacher.FullName)
                            .FirstOrDefault(),
                        group.AcademicLevelId,
                        context.AcademicLevels
                            .Where(level => level.Id == group.AcademicLevelId)
                            .Select(level => level.Name)
                            .FirstOrDefault(),
                        group.GradeId,
                        context.Grades
                            .Where(grade => grade.Id == group.GradeId)
                            .Select(grade => grade.Name)
                            .FirstOrDefault(),
                        group.MaxStudents,
                        group.MonthlyFee,
                        group.IsActive,
                        context.Enrollments.Count(enrollment =>
                            enrollment.GroupId == group.Id
                            && enrollment.Status == EnrollmentStatuses.Active),
                        context.ScheduleTemplates.Count(template =>
                            template.GroupId == group.Id
                            && template.IsActive)))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken);
    }

    public Task<Result<GroupSummary?>> GetSummaryAsync(
        int groupId,
        CancellationToken cancellationToken = default)
        => RunAsync<GroupSummary?>(
            "Groups.GetSummary",
            async context => await Project(
                    context,
                    context.Groups.AsNoTracking().Where(group => group.Id == groupId))
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    public Task<Result<GroupSaveOutcome>> SaveAsync(
        GroupSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RunResultAsync(
            "Groups.Save",
            async context =>
            {
                var now = Clock.LocalNow;
                Group group;
                var created = request.GroupId is null or <= 0;

                if (request.GroupId is { } id and > 0)
                {
                    var existing = await context.Groups
                        .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                        .ConfigureAwait(false);
                    if (existing is null)
                    {
                        return Result<GroupSaveOutcome>.Fail(
                            Failure.NotFound(TranslationKeys.Errors.NotFound));
                    }

                    group = existing;
                }
                else
                {
                    group = new Group
                    {
                        CreatedAt = now,
                        UpdatedAt = now,
                    };
                    context.Groups.Add(group);
                }

                var levelId = request.AcademicLevelId is > 0
                    ? request.AcademicLevelId
                    : await context.Grades
                        .AsNoTracking()
                        .Where(grade => grade.Id == request.GradeId)
                        .Select(grade => grade.AcademicLevelId)
                        .FirstOrDefaultAsync(cancellationToken)
                        .ConfigureAwait(false);

                group.Name = request.Name.Trim();
                group.AcademicLevelId = levelId;
                group.GradeId = request.GradeId;
                group.SubjectId = request.SubjectId;
                group.TeacherId = request.TeacherId;
                group.MaxStudents = request.MaxStudents;
                group.MonthlyFee = request.MonthlyFee;
                group.IsActive = request.IsActive;
                group.UpdatedAt = now;

                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return Result<GroupSaveOutcome>.Success(
                    new GroupSaveOutcome(group.Id, created));
            },
            cancellationToken,
            TranslationKeys.Errors.SaveError);
    }

    public Task<Result<GroupDependencies>> GetDependenciesAsync(
        int groupId,
        CancellationToken cancellationToken = default)
        => RunAsync(
            "Groups.GetDependencies",
            async context =>
            {
                var enrollments = await context.Enrollments
                    .AsNoTracking()
                    .CountAsync(x => x.GroupId == groupId, cancellationToken)
                    .ConfigureAwait(false);
                var sessions = await context.Sessions
                    .AsNoTracking()
                    .CountAsync(x => x.GroupId == groupId, cancellationToken)
                    .ConfigureAwait(false);
                var slots = await context.ScheduleTemplates
                    .AsNoTracking()
                    .CountAsync(x => x.GroupId == groupId, cancellationToken)
                    .ConfigureAwait(false);

                return new GroupDependencies(enrollments, sessions, slots);
            },
            cancellationToken);

    public Task<Result<int>> GetActiveEnrollmentCountAsync(
        int groupId,
        CancellationToken cancellationToken = default)
        => RunAsync(
            "Groups.GetActiveEnrollmentCount",
            async context => await context.Enrollments
                .AsNoTracking()
                .CountAsync(
                    enrollment => enrollment.GroupId == groupId
                        && enrollment.Status == EnrollmentStatuses.Active,
                    cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    private static IQueryable<Group> ApplySort(
        SchoolManagementDbContext context,
        IQueryable<Group> groups,
        GroupSortField field,
        bool descending)
    {
        IOrderedQueryable<Group> ordered = field switch
        {
            GroupSortField.Subject => descending
                ? groups.OrderByDescending(group => context.Subjects
                    .Where(subject => subject.Id == group.SubjectId)
                    .Select(subject => subject.Name)
                    .FirstOrDefault())
                : groups.OrderBy(group => context.Subjects
                    .Where(subject => subject.Id == group.SubjectId)
                    .Select(subject => subject.Name)
                    .FirstOrDefault()),
            GroupSortField.Teacher => descending
                ? groups.OrderByDescending(group => context.Teachers
                    .Where(teacher => teacher.Id == group.TeacherId)
                    .Select(teacher => teacher.FullName)
                    .FirstOrDefault())
                : groups.OrderBy(group => context.Teachers
                    .Where(teacher => teacher.Id == group.TeacherId)
                    .Select(teacher => teacher.FullName)
                    .FirstOrDefault()),
            GroupSortField.Grade => descending
                ? groups.OrderByDescending(group => context.Grades
                    .Where(grade => grade.Id == group.GradeId)
                    .Select(grade => grade.Name)
                    .FirstOrDefault())
                : groups.OrderBy(group => context.Grades
                    .Where(grade => grade.Id == group.GradeId)
                    .Select(grade => grade.Name)
                    .FirstOrDefault()),
            GroupSortField.Capacity => descending
                ? groups.OrderByDescending(group => group.MaxStudents)
                : groups.OrderBy(group => group.MaxStudents),
            GroupSortField.Fee => descending
                ? groups.OrderByDescending(group => group.MonthlyFee)
                : groups.OrderBy(group => group.MonthlyFee),
            _ => descending
                ? groups.OrderByDescending(group => group.Name)
                : groups.OrderBy(group => group.Name),
        };

        return ordered.ThenBy(group => group.Name);
    }

    private static IQueryable<GroupSummary> Project(
        SchoolManagementDbContext context,
        IQueryable<Group> groups)
        => groups.Select(group => new GroupSummary(
            group.Id,
            group.Name,
            group.SubjectId,
            context.Subjects
                .Where(subject => subject.Id == group.SubjectId)
                .Select(subject => subject.Name)
                .FirstOrDefault(),
            group.TeacherId,
            context.Teachers
                .Where(teacher => teacher.Id == group.TeacherId)
                .Select(teacher => teacher.FullName)
                .FirstOrDefault(),
            group.AcademicLevelId,
            context.AcademicLevels
                .Where(level => level.Id == group.AcademicLevelId)
                .Select(level => level.Name)
                .FirstOrDefault(),
            group.GradeId,
            context.Grades
                .Where(grade => grade.Id == group.GradeId)
                .Select(grade => grade.Name)
                .FirstOrDefault(),
            group.MaxStudents,
            group.MonthlyFee,
            group.IsActive,
            context.Enrollments.Count(enrollment =>
                enrollment.GroupId == group.Id
                && enrollment.Status == EnrollmentStatuses.Active),
            context.ScheduleTemplates.Count(template =>
                template.GroupId == group.Id
                && template.IsActive)));
}
