using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Academic;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Time;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

public sealed class SubjectRepository : GenericRepository<Subject>, ISubjectRepository
{
    public SubjectRepository(
        IDbContextFactory<SchoolManagementDbContext> contextFactory,
        ITrustedClock clock,
        ILogger<SubjectRepository> logger)
        : base(contextFactory, clock, logger)
    {
    }

    public Task<Result<IReadOnlyList<SubjectListItem>>> GetListAsync(
        AcademicListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return RunAsync<IReadOnlyList<SubjectListItem>>(
            "Subjects.GetList",
            async context =>
            {
                var subjects = context.Subjects.AsNoTracking();

                if (!query.IncludeInactive)
                {
                    subjects = subjects.Where(subject => subject.IsActive);
                }

                var term = AcademicSearch.Normalize(query.SearchTerm);
                if (term is not null)
                {
                    subjects = subjects.Where(subject =>
                        subject.Name.Contains(term)
                        || (subject.Code != null && subject.Code.Contains(term))
                        || (subject.Description != null && subject.Description.Contains(term)));
                }

                var ordered = query.SortBy switch
                {
                    AcademicSortField.Order => query.Descending
                        ? subjects.OrderByDescending(subject => subject.Code).ThenByDescending(subject => subject.Name)
                        : subjects.OrderBy(subject => subject.Code).ThenBy(subject => subject.Name),
                    AcademicSortField.LinkedItems => query.Descending
                        ? subjects
                            .OrderByDescending(subject => context.Groups.Count(group => group.SubjectId == subject.Id))
                            .ThenByDescending(subject => subject.Name)
                        : subjects
                            .OrderBy(subject => context.Groups.Count(group => group.SubjectId == subject.Id))
                            .ThenBy(subject => subject.Name),
                    _ => query.Descending
                        ? subjects.OrderByDescending(subject => subject.Name)
                        : subjects.OrderBy(subject => subject.Name),
                };

                return await ordered
                    .Select(subject => new SubjectListItem(
                        subject.Id,
                        subject.Name,
                        subject.Code,
                        subject.Description,
                        subject.IsActive,
                        context.GradeSubjects.Count(link => link.SubjectId == subject.Id),
                        context.Groups.Count(group => group.SubjectId == subject.Id)))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken);
    }

    public Task<Result<bool>> CodeExistsAsync(
        string code,
        int? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Task.FromResult(Result<bool>.Success(false));
        }

        var normalized = code.Trim().ToLowerInvariant();

        return RunAsync(
            "Subjects.CodeExists",
            async context =>
            {
                var query = context.Subjects
                    .AsNoTracking()
                    .Where(subject => subject.Code != null && subject.Code.ToLower() == normalized);

                if (excludeId is not null)
                {
                    query = query.Where(subject => subject.Id != excludeId);
                }

                return await query.AnyAsync(cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
    }

    public Task<Result<int>> CountGroupsAsync(
        int subjectId,
        CancellationToken cancellationToken = default)
        => RunAsync(
            "Subjects.CountGroups",
            async context => await context.Groups
                .AsNoTracking()
                .CountAsync(group => group.SubjectId == subjectId, cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    public Task<Result<int>> CountGradesAsync(
        int subjectId,
        CancellationToken cancellationToken = default)
        => RunAsync(
            "Subjects.CountGrades",
            async context => await context.GradeSubjects
                .AsNoTracking()
                .CountAsync(link => link.SubjectId == subjectId, cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);
}
