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

public sealed class EnrollmentRepository : GenericRepository<Enrollment>, IEnrollmentRepository
{
    private static readonly SemaphoreSlim EnrollmentGate = new(1, 1);

    public EnrollmentRepository(
        IDbContextFactory<SchoolManagementDbContext> contextFactory,
        ITrustedClock clock,
        ILogger<EnrollmentRepository> logger)
        : base(contextFactory, clock, logger)
    {
    }

    public Task<Result<IReadOnlyList<EnrollmentListItem>>> GetByStudentAsync(
        int studentId,
        CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<EnrollmentListItem>>(
            "Enrollments.GetByStudent",
            async context => await (
                    from enrollment in context.Enrollments.AsNoTracking()
                    join student in context.Students.AsNoTracking() on enrollment.StudentId equals student.Id
                    join groupEntity in context.Groups.AsNoTracking() on enrollment.GroupId equals groupEntity.Id
                    where enrollment.StudentId == studentId
                    orderby enrollment.EnrollmentDate descending, enrollment.Id descending
                    select new EnrollmentListItem(
                        enrollment.Id,
                        enrollment.StudentId,
                        student.StudentCode,
                        student.FirstName + " " + student.LastName,
                        enrollment.GroupId,
                        groupEntity.Name,
                        enrollment.EnrollmentDate,
                        enrollment.Status,
                        enrollment.RegistrationFee))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    public Task<Result<IReadOnlyList<EnrollmentListItem>>> GetByGroupAsync(
        int groupId,
        CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<EnrollmentListItem>>(
            "Enrollments.GetByGroup",
            async context => await (
                    from enrollment in context.Enrollments.AsNoTracking()
                    join student in context.Students.AsNoTracking() on enrollment.StudentId equals student.Id
                    join groupEntity in context.Groups.AsNoTracking() on enrollment.GroupId equals groupEntity.Id
                    where enrollment.GroupId == groupId
                    orderby student.LastName, student.FirstName
                    select new EnrollmentListItem(
                        enrollment.Id,
                        enrollment.StudentId,
                        student.StudentCode,
                        student.FirstName + " " + student.LastName,
                        enrollment.GroupId,
                        groupEntity.Name,
                        enrollment.EnrollmentDate,
                        enrollment.Status,
                        enrollment.RegistrationFee))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    public Task<Result<int>> CountActiveByGroupAsync(
        int groupId,
        CancellationToken cancellationToken = default)
        => RunAsync(
            "Enrollments.CountActiveByGroup",
            async context => await context.Enrollments
                .AsNoTracking()
                .CountAsync(
                    enrollment => enrollment.GroupId == groupId
                        && enrollment.Status == EnrollmentStatuses.Active,
                    cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    public Task<Result<bool>> ExistsActiveAsync(
        int groupId,
        int studentId,
        CancellationToken cancellationToken = default)
        => RunAsync(
            "Enrollments.ExistsActive",
            async context => await context.Enrollments
                .AsNoTracking()
                .AnyAsync(
                    enrollment => enrollment.GroupId == groupId
                        && enrollment.StudentId == studentId
                        && enrollment.Status == EnrollmentStatuses.Active,
                    cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    public async Task<Result<SchoolManagement.Application.Groups.EnrollmentSaveOutcome>> EnrollAsync(
        SchoolManagement.Application.Groups.EnrollmentSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await EnrollmentGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await RunResultAsync(
                "Enrollments.Enroll",
                async context =>
                {
                    var now = Clock.LocalNow;
                    var existing = await context.Enrollments
                        .FirstOrDefaultAsync(
                            enrollment => enrollment.GroupId == request.GroupId
                                && enrollment.StudentId == request.StudentId
                                && enrollment.Status == EnrollmentStatuses.Active,
                            cancellationToken)
                        .ConfigureAwait(false);

                    if (existing is not null)
                    {
                        return Result<SchoolManagement.Application.Groups.EnrollmentSaveOutcome>.Success(
                            new SchoolManagement.Application.Groups.EnrollmentSaveOutcome(
                                existing.Id,
                                Created: false,
                                AlreadyEnrolled: true));
                    }

                    var today = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
                    var enrollment = new Enrollment
                    {
                        SchoolId = 1,
                        StudentId = request.StudentId,
                        GroupId = request.GroupId,
                        EnrollmentDate = today,
                        RegistrationFee = 0,
                        Status = EnrollmentStatuses.Active,
                        Notes = request.Notes,
                        CreatedAt = now,
                        UpdatedAt = now,
                    };

                    context.Enrollments.Add(enrollment);
                    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    return Result<SchoolManagement.Application.Groups.EnrollmentSaveOutcome>.Success(
                        new SchoolManagement.Application.Groups.EnrollmentSaveOutcome(
                            enrollment.Id,
                            Created: true,
                            AlreadyEnrolled: false));
                },
                cancellationToken,
                TranslationKeys.Errors.SaveError).ConfigureAwait(false);
        }
        finally
        {
            EnrollmentGate.Release();
        }
    }

    public Task<Result<bool>> SetStatusAsync(
        int enrollmentId,
        string status,
        CancellationToken cancellationToken = default)
        => RunResultAsync(
            "Enrollments.SetStatus",
            async context =>
            {
                var enrollment = await context.Enrollments
                    .FirstOrDefaultAsync(x => x.Id == enrollmentId, cancellationToken)
                    .ConfigureAwait(false);

                if (enrollment is null)
                {
                    return Result<bool>.Fail(Failure.NotFound(TranslationKeys.Errors.NotFound));
                }

                enrollment.Status = status;
                enrollment.UpdatedAt = Clock.LocalNow;
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return Result<bool>.Success(true);
            },
            cancellationToken,
            TranslationKeys.Errors.SaveError);
}
