using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Time;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>
/// Small lookup lists used by combo boxes. Every method uses a projection so
/// only the id and the display name leave SQLite.
/// </summary>
public sealed class ReferenceDataRepository : RepositoryBase, IReferenceDataRepository
{
    public ReferenceDataRepository(
        IDbContextFactory<SchoolManagementDbContext> contextFactory,
        ITrustedClock clock,
        ILogger<ReferenceDataRepository> logger)
        : base(contextFactory, clock, logger)
    {
    }

    public Task<Result<IReadOnlyList<LookupItem>>> GetSubjectsAsync(
        bool onlyActive,
        CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<LookupItem>>(
            "ReferenceData.GetSubjects",
            async context =>
            {
                var query = context.Subjects.AsNoTracking();
                if (onlyActive)
                {
                    query = query.Where(subject => subject.IsActive);
                }

                return await query
                    .OrderBy(subject => subject.Name)
                    .Select(subject => new LookupItem(subject.Id, subject.Name))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken);

    public Task<Result<IReadOnlyList<LookupItem>>> GetTeachersAsync(
        bool onlyActive,
        CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<LookupItem>>(
            "ReferenceData.GetTeachers",
            async context =>
            {
                var query = context.Teachers.AsNoTracking();
                if (onlyActive)
                {
                    query = query.Where(teacher => teacher.IsActive);
                }

                return await query
                    .OrderBy(teacher => teacher.FullName)
                    .Select(teacher => new LookupItem(teacher.Id, teacher.FullName))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken);

    public Task<Result<IReadOnlyList<LookupItem>>> GetGuardiansAsync(
        bool onlyActive,
        CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<LookupItem>>(
            "ReferenceData.GetGuardians",
            async context =>
            {
                var query = context.Guardians.AsNoTracking();
                if (onlyActive)
                {
                    query = query.Where(guardian => guardian.IsActive);
                }

                return await query
                    .OrderBy(guardian => guardian.FullName)
                    .Select(guardian => new LookupItem(guardian.Id, guardian.FullName))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken);

    public Task<Result<IReadOnlyList<LookupItem>>> GetAcademicLevelsAsync(
        bool onlyActive,
        CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<LookupItem>>(
            "ReferenceData.GetAcademicLevels",
            async context =>
            {
                var query = context.AcademicLevels.AsNoTracking();
                if (onlyActive)
                {
                    query = query.Where(level => level.IsActive);
                }

                return await query
                    .OrderBy(level => level.OrderIndex)
                    .ThenBy(level => level.Name)
                    .Select(level => new LookupItem(level.Id, level.Name))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken);

    public Task<Result<IReadOnlyList<LookupItem>>> GetGradesAsync(
        int? academicLevelId,
        bool onlyActive,
        CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<LookupItem>>(
            "ReferenceData.GetGrades",
            async context =>
            {
                var query = context.Grades.AsNoTracking();

                if (academicLevelId is not null)
                {
                    query = query.Where(grade => grade.AcademicLevelId == academicLevelId);
                }

                if (onlyActive)
                {
                    query = query.Where(grade => grade.IsActive);
                }

                return await query
                    .OrderBy(grade => grade.OrderIndex)
                    .ThenBy(grade => grade.Name)
                    .Select(grade => new LookupItem(grade.Id, grade.Name))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken);

    public Task<Result<string>> GetSchoolNameAsync(CancellationToken cancellationToken = default)
        => RunAsync(
            "ReferenceData.GetSchoolName",
            async context => await context.Schools
                .AsNoTracking()
                .OrderBy(school => school.Id)
                .Select(school => school.Name)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false) ?? string.Empty,
            cancellationToken);
}
