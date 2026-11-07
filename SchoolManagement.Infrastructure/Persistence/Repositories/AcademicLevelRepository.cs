using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Academic;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Time;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>
/// Levels are ordered by <c>order_index</c> then name. Linked counts are
/// correlated subqueries, so the screen receives one row per level without a
/// second round-trip.
/// </summary>
public sealed class AcademicLevelRepository : GenericRepository<AcademicLevel>, IAcademicLevelRepository
{
    public AcademicLevelRepository(
        IDbContextFactory<SchoolManagementDbContext> contextFactory,
        ITrustedClock clock,
        ILogger<AcademicLevelRepository> logger)
        : base(contextFactory, clock, logger)
    {
    }

    public Task<Result<IReadOnlyList<AcademicLevelListItem>>> GetListAsync(
        AcademicListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return RunAsync<IReadOnlyList<AcademicLevelListItem>>(
            "AcademicLevels.GetList",
            async context =>
            {
                var levels = context.AcademicLevels.AsNoTracking();

                if (!query.IncludeInactive)
                {
                    levels = levels.Where(level => level.IsActive);
                }

                var term = AcademicSearch.Normalize(query.SearchTerm);
                if (term is not null)
                {
                    levels = levels.Where(level => level.Name.Contains(term));
                }

                var ordered = query.SortBy switch
                {
                    AcademicSortField.Order => query.Descending
                        ? levels.OrderByDescending(level => level.OrderIndex).ThenByDescending(level => level.Name)
                        : levels.OrderBy(level => level.OrderIndex).ThenBy(level => level.Name),
                    AcademicSortField.LinkedItems => query.Descending
                        ? levels
                            .OrderByDescending(level => context.Grades.Count(grade => grade.AcademicLevelId == level.Id))
                            .ThenByDescending(level => level.Name)
                        : levels
                            .OrderBy(level => context.Grades.Count(grade => grade.AcademicLevelId == level.Id))
                            .ThenBy(level => level.Name),
                    _ => query.Descending
                        ? levels.OrderByDescending(level => level.Name)
                        : levels.OrderBy(level => level.Name),
                };

                return await ordered
                    .Select(level => new AcademicLevelListItem(
                        level.Id,
                        level.Name,
                        level.OrderIndex,
                        level.IsActive,
                        context.Grades.Count(grade => grade.AcademicLevelId == level.Id),
                        context.Groups.Count(group => group.AcademicLevelId == level.Id)))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken);
    }

    public Task<Result<int>> CountGradesAsync(
        int levelId,
        CancellationToken cancellationToken = default)
        => RunAsync(
            "AcademicLevels.CountGrades",
            async context => await context.Grades
                .AsNoTracking()
                .CountAsync(grade => grade.AcademicLevelId == levelId, cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    public Task<Result<int>> CountGroupsAsync(
        int levelId,
        CancellationToken cancellationToken = default)
        => RunAsync(
            "AcademicLevels.CountGroups",
            async context => await context.Groups
                .AsNoTracking()
                .CountAsync(group => group.AcademicLevelId == levelId, cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);
}

internal static class AcademicSearch
{
    public static string? Normalize(string? searchTerm) =>
        string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim();
}
