using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Time;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Policies;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

public sealed class StudentRepository : RepositoryBase, IStudentRepository
{
    public StudentRepository(
        IDbContextFactory<SchoolManagementDbContext> contextFactory,
        ITrustedClock clock,
        ILogger<StudentRepository> logger)
        : base(contextFactory, clock, logger)
    {
    }

    public Task<Result<Student?>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => RunAsync<Student?>(
            "Students.GetById",
            async context => await context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(student => student.Id == id, cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    public Task<Result<int>> AddAsync(Student entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return RunAsync(
            "Students.Add",
            async context =>
            {
                RepositoryHelper.ApplyCreatedTimestamps(entity, Clock.LocalNow);
                context.Students.Add(entity);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return entity.Id;
            },
            cancellationToken,
            TranslationKeys.Errors.SaveError);
    }

    public Task<Result<bool>> UpdateAsync(Student entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return RunResultAsync(
            "Students.Update",
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
            "Students.Delete",
            async context =>
            {
                var student = await context.Students
                    .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                    .ConfigureAwait(false);

                if (student is null)
                {
                    return Result<bool>.Fail(Failure.NotFound(TranslationKeys.Errors.NotFound));
                }

                context.Students.Remove(student);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return Result<bool>.Success(true);
            },
            cancellationToken,
            TranslationKeys.Errors.SaveError);

    public Task<Result<IReadOnlyList<StudentListItem>>> SearchAsync(
        string? searchTerm,
        bool onlyActive,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var term = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim();
        var offset = Math.Max(0, skip);
        var pageSize = Math.Clamp(take, 0, 500);

        return RunAsync<IReadOnlyList<StudentListItem>>(
            "Students.Search",
            async context =>
            {
                var query = context.Students.AsNoTracking();

                if (onlyActive)
                {
                    query = query.Where(student => student.Status == StudentStatuses.Active);
                }

                if (term is not null)
                {
                    var pattern = $"%{term}%";
                    query = query.Where(student =>
                        EF.Functions.Like(student.FirstName, pattern)
                        || EF.Functions.Like(student.LastName, pattern)
                        || EF.Functions.Like(student.StudentCode, pattern)
                        || EF.Functions.Like(student.FirstName + " " + student.LastName, pattern));
                }

                return await query
                    .OrderBy(student => student.LastName)
                    .ThenBy(student => student.FirstName)
                    .Skip(offset)
                    .Take(pageSize)
                    .Select(student => new StudentListItem(
                        student.Id,
                        student.StudentCode,
                        student.FirstName,
                        student.LastName,
                        student.Phone,
                        student.Status,
                        context.Guardians
                            .Where(guardian => guardian.Id == student.GuardianId)
                            .Select(guardian => guardian.FullName)
                            .FirstOrDefault(),
                        student.GuardianId))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken);
    }

    public Task<Result<StudentDetails?>> GetDetailsAsync(
        int id,
        CancellationToken cancellationToken = default)
        => RunAsync<StudentDetails?>(
            "Students.GetDetails",
            async context => await context.Students
                .AsNoTracking()
                .Where(student => student.Id == id)
                .Select(student => new StudentDetails(
                    student.Id,
                    student.StudentCode,
                    student.FirstName,
                    student.LastName,
                    student.DateOfBirth,
                    student.Gender,
                    student.Address,
                    student.Phone,
                    student.PhotoPath,
                    student.Status,
                    student.Notes,
                    student.RegistrationFee,
                    student.GuardianId,
                    context.Guardians
                        .Where(guardian => guardian.Id == student.GuardianId)
                        .Select(guardian => guardian.FullName)
                        .FirstOrDefault(),
                    context.Guardians
                        .Where(guardian => guardian.Id == student.GuardianId)
                        .Select(guardian => guardian.Phone)
                        .FirstOrDefault()))
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    public Task<Result<int>> CountAsync(bool onlyActive, CancellationToken cancellationToken = default)
        => RunAsync(
            "Students.Count",
            async context =>
            {
                var query = context.Students.AsNoTracking();
                if (onlyActive)
                {
                    query = query.Where(student => student.Status == StudentStatuses.Active);
                }

                return await query.CountAsync(cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);

    public Task<Result<StudentAbsenceSummary>> GetAbsenceSummaryAsync(
        int studentId,
        CancellationToken cancellationToken = default)
        => RunAsync(
            "Students.GetAbsenceSummary",
            async context =>
            {
                var limit = await ReadAbsenceLimitAsync(context, cancellationToken).ConfigureAwait(false);
                var absenceCount = await context.Attendance
                    .AsNoTracking()
                    .CountAsync(
                        record => record.StudentId == studentId
                            && (record.Status == AttendanceStatuses.Absent
                                || record.Status == AttendanceStatuses.Unexcused),
                        cancellationToken)
                    .ConfigureAwait(false);

                var status = AbsenceLimitPolicy.Evaluate(absenceCount, limit);
                return new StudentAbsenceSummary(
                    studentId,
                    absenceCount,
                    limit,
                    status.HasReachedLimit,
                    status.IsExceeded);
            },
            cancellationToken);

    private static async Task<int> ReadAbsenceLimitAsync(
        SchoolManagementDbContext context,
        CancellationToken cancellationToken)
    {
        var storedValue = await context.SystemSettings
            .AsNoTracking()
            .Where(setting => setting.Key == AbsenceLimitPolicy.SettingKey)
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return AbsenceLimitPolicy.ParseLimit(storedValue);
    }
}
