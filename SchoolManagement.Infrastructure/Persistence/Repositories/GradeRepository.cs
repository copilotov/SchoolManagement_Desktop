using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Academic;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Time;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>
/// Grades carry both a level name and two linked counts. Deleting a grade
/// removes its <c>grade_subjects</c> configuration in the same transaction;
/// groups that reference the grade are blocked in the use case.
/// </summary>
public sealed class GradeRepository : GenericRepository<Grade>, IGradeRepository
{
    public GradeRepository(
        IDbContextFactory<SchoolManagementDbContext> contextFactory,
        ITrustedClock clock,
        ILogger<GradeRepository> logger)
        : base(contextFactory, clock, logger)
    {
    }

    public Task<Result<IReadOnlyList<GradeListItem>>> GetListAsync(
        AcademicListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return RunAsync<IReadOnlyList<GradeListItem>>(
            "Grades.GetList",
            async context =>
            {
                var grades = context.Grades.AsNoTracking();

                if (!query.IncludeInactive)
                {
                    grades = grades.Where(grade => grade.IsActive);
                }

                var term = AcademicSearch.Normalize(query.SearchTerm);
                if (term is not null)
                {
                    grades = grades.Where(grade => grade.Name.Contains(term));
                }

                var ordered = query.SortBy switch
                {
                    AcademicSortField.Order => query.Descending
                        ? grades.OrderByDescending(grade => grade.OrderIndex).ThenByDescending(grade => grade.Name)
                        : grades.OrderBy(grade => grade.OrderIndex).ThenBy(grade => grade.Name),
                    AcademicSortField.LinkedItems => query.Descending
                        ? grades
                            .OrderByDescending(grade => context.Groups.Count(group => group.GradeId == grade.Id))
                            .ThenByDescending(grade => grade.Name)
                        : grades
                            .OrderBy(grade => context.Groups.Count(group => group.GradeId == grade.Id))
                            .ThenBy(grade => grade.Name),
                    _ => query.Descending
                        ? grades.OrderByDescending(grade => grade.Name)
                        : grades.OrderBy(grade => grade.Name),
                };

                return await ordered
                    .Select(grade => new GradeListItem(
                        grade.Id,
                        grade.Name,
                        grade.AcademicLevelId,
                        context.AcademicLevels
                            .Where(level => level.Id == grade.AcademicLevelId)
                            .Select(level => level.Name)
                            .FirstOrDefault(),
                        grade.OrderIndex,
                        grade.IsActive,
                        context.GradeSubjects.Count(link => link.GradeId == grade.Id),
                        context.Groups.Count(group => group.GradeId == grade.Id)))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken);
    }

    public Task<Result<IReadOnlyList<int>>> GetSubjectIdsAsync(
        int gradeId,
        CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<int>>(
            "Grades.GetSubjectIds",
            async context => await context.GradeSubjects
                .AsNoTracking()
                .Where(link => link.GradeId == gradeId)
                .OrderBy(link => link.SubjectId)
                .Select(link => link.SubjectId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    public Task<Result<bool>> SetSubjectsAsync(
        int gradeId,
        IReadOnlyCollection<int> subjectIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subjectIds);

        return RunResultAsync(
            "Grades.SetSubjects",
            async context =>
            {
                var gradeExists = await context.Grades
                    .AsNoTracking()
                    .AnyAsync(grade => grade.Id == gradeId, cancellationToken)
                    .ConfigureAwait(false);

                if (!gradeExists)
                {
                    return Result<bool>.Fail(Failure.NotFound(TranslationKeys.Errors.NotFound));
                }

                // List<int>, not int[]: see the note in StudentRepository. Under
                // C# 14 an array .Contains binds to the ReadOnlySpan overload,
                // which EF Core's expression interpreter cannot compile.
                var desired = subjectIds.Distinct().ToList();
                IReadOnlyList<int> validSubjectIds = desired.Count == 0
                    ? Array.Empty<int>()
                    : await context.Subjects
                        .AsNoTracking()
                        .Where(subject => desired.Contains(subject.Id))
                        .Select(subject => subject.Id)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);

                var existing = await context.GradeSubjects
                    .Where(link => link.GradeId == gradeId)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var validSet = validSubjectIds.ToHashSet();
                var removed = existing.Where(link => !validSet.Contains(link.SubjectId)).ToList();
                context.GradeSubjects.RemoveRange(removed);

                var existingSet = existing.Select(link => link.SubjectId).ToHashSet();
                var now = Clock.LocalNow;
                foreach (var subjectId in validSubjectIds.Where(id => !existingSet.Contains(id)))
                {
                    context.GradeSubjects.Add(new GradeSubject
                    {
                        GradeId = gradeId,
                        SubjectId = subjectId,
                        CreatedAt = now,
                    });
                }

                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return Result<bool>.Success(true);
            },
            cancellationToken,
            TranslationKeys.Errors.SaveGradeSubjects);
    }

    public Task<Result<int>> CountGroupsAsync(
        int gradeId,
        CancellationToken cancellationToken = default)
        => RunAsync(
            "Grades.CountGroups",
            async context => await context.Groups
                .AsNoTracking()
                .CountAsync(group => group.GradeId == gradeId, cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    public override Task<Result<bool>> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
        => RunResultAsync(
            "Grades.Delete",
            async context =>
            {
                var grade = await context.Grades
                    .FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken)
                    .ConfigureAwait(false);

                if (grade is null)
                {
                    return Result<bool>.Fail(Failure.NotFound(TranslationKeys.Errors.NotFound));
                }

                var links = await context.GradeSubjects
                    .Where(link => link.GradeId == id)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                context.GradeSubjects.RemoveRange(links);
                context.Grades.Remove(grade);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return Result<bool>.Success(true);
            },
            cancellationToken,
            TranslationKeys.Errors.SaveError);
}
