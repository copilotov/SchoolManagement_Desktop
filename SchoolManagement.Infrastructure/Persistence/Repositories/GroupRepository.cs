using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Common;
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
        => RunAsync<IReadOnlyList<GroupSummary>>(
            "Groups.GetSummaries",
            async context =>
            {
                var query = context.Groups.AsNoTracking();
                if (!includeInactive)
                {
                    query = query.Where(group => group.IsActive);
                }

                return await query
                    .OrderBy(group => group.Name)
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
                            && enrollment.Status == EnrollmentStatuses.Active)))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
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
}
