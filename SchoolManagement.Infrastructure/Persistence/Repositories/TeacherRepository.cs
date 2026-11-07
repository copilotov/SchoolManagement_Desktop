using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Academic;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Time;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

public sealed class TeacherRepository : GenericRepository<Teacher>, ITeacherRepository
{
    public TeacherRepository(
        IDbContextFactory<SchoolManagementDbContext> contextFactory,
        ITrustedClock clock,
        ILogger<TeacherRepository> logger)
        : base(contextFactory, clock, logger)
    {
    }

    public Task<Result<IReadOnlyList<TeacherListItem>>> GetListAsync(
        AcademicListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return RunAsync<IReadOnlyList<TeacherListItem>>(
            "Teachers.GetList",
            async context =>
            {
                var teachers = context.Teachers.AsNoTracking();

                if (!query.IncludeInactive)
                {
                    teachers = teachers.Where(teacher => teacher.IsActive);
                }

                var term = AcademicSearch.Normalize(query.SearchTerm);
                if (term is not null)
                {
                    teachers = teachers.Where(teacher =>
                        teacher.FullName.Contains(term)
                        || (teacher.Phone != null && teacher.Phone.Contains(term))
                        || (teacher.Email != null && teacher.Email.Contains(term))
                        || (teacher.Specialization != null && teacher.Specialization.Contains(term)));
                }

                var ordered = query.SortBy switch
                {
                    AcademicSortField.Order => query.Descending
                        ? teachers.OrderByDescending(teacher => teacher.HireDate).ThenByDescending(teacher => teacher.FullName)
                        : teachers.OrderBy(teacher => teacher.HireDate).ThenBy(teacher => teacher.FullName),
                    AcademicSortField.LinkedItems => query.Descending
                        ? teachers
                            .OrderByDescending(teacher => context.Groups.Count(group => group.TeacherId == teacher.Id))
                            .ThenByDescending(teacher => teacher.FullName)
                        : teachers
                            .OrderBy(teacher => context.Groups.Count(group => group.TeacherId == teacher.Id))
                            .ThenBy(teacher => teacher.FullName),
                    _ => query.Descending
                        ? teachers.OrderByDescending(teacher => teacher.FullName)
                        : teachers.OrderBy(teacher => teacher.FullName),
                };

                return await ordered
                    .Select(teacher => new TeacherListItem(
                        teacher.Id,
                        teacher.FullName,
                        teacher.Phone,
                        teacher.Email,
                        teacher.Address,
                        teacher.Specialization,
                        teacher.HireDate,
                        teacher.IsActive,
                        context.Groups.Count(group => group.TeacherId == teacher.Id)))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken);
    }

    public Task<Result<int>> CountGroupsAsync(
        int teacherId,
        CancellationToken cancellationToken = default)
        => RunAsync(
            "Teachers.CountGroups",
            async context => await context.Groups
                .AsNoTracking()
                .CountAsync(group => group.TeacherId == teacherId, cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);
}
