using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Homeworks;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Time;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Policies;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

public sealed class HomeworkRepository : GenericRepository<Homework>, IHomeworkRepository
{
    public HomeworkRepository(
        IDbContextFactory<SchoolManagementDbContext> contextFactory,
        ITrustedClock clock,
        ILogger<HomeworkRepository> logger)
        : base(contextFactory, clock, logger)
    {
    }

    public Task<Result<IReadOnlyList<HomeworkListItem>>> GetByGroupAsync(
        int groupId,
        CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<HomeworkListItem>>(
            "Homeworks.GetByGroup",
            async context => await context.Homeworks
                .AsNoTracking()
                .Where(homework => homework.GroupId == groupId)
                .OrderByDescending(homework => homework.DueDate)
                .ThenByDescending(homework => homework.Id)
                .Select(homework => new HomeworkListItem(
                    homework.Id,
                    homework.GroupId,
                    homework.SessionId,
                    homework.TargetSessionId,
                    homework.Title,
                    homework.Description,
                    homework.DueDate,
                    homework.IsCompleted,
                    context.HomeworkSubmissions.Count(submission =>
                        submission.HomeworkId == homework.Id),
                    context.HomeworkSubmissions.Count(submission =>
                        submission.HomeworkId == homework.Id
                        && (submission.Status == HomeworkStatuses.Submitted
                            || submission.Status == HomeworkStatuses.Late)),
                    context.HomeworkSubmissions.Count(submission =>
                        submission.HomeworkId == homework.Id
                        && (submission.Status == HomeworkStatuses.Pending
                            || submission.Status == HomeworkStatuses.Missing))))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    public Task<Result<IReadOnlyList<HomeworkSubmissionItem>>> GetSubmissionsAsync(
        int homeworkId,
        CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<HomeworkSubmissionItem>>(
            "Homeworks.GetSubmissions",
            async context => await (
                    from submission in context.HomeworkSubmissions.AsNoTracking()
                    join student in context.Students.AsNoTracking()
                        on submission.StudentId equals student.Id
                    where submission.HomeworkId == homeworkId
                    orderby student.LastName, student.FirstName
                    select new HomeworkSubmissionItem(
                        submission.Id,
                        submission.HomeworkId,
                        submission.StudentId,
                        student.StudentCode,
                        student.FirstName + " " + student.LastName,
                        submission.Status,
                        submission.SubmittedAt,
                        submission.Notes))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    public Task<Result<bool>> SetSubmissionStatusAsync(
        int homeworkId,
        int studentId,
        string status,
        DateTime? submittedAt,
        string? notes,
        CancellationToken cancellationToken = default)
        => RunResultAsync(
            "Homeworks.SetSubmissionStatus",
            async context =>
            {
                var now = Clock.LocalNow;
                var submission = await context.HomeworkSubmissions
                    .FirstOrDefaultAsync(
                        x => x.HomeworkId == homeworkId && x.StudentId == studentId,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (submission is null)
                {
                    submission = new HomeworkSubmission
                    {
                        HomeworkId = homeworkId,
                        StudentId = studentId,
                        Status = status,
                        SubmittedAt = submittedAt,
                        Notes = notes,
                        CreatedAt = now,
                        UpdatedAt = now,
                    };
                    context.HomeworkSubmissions.Add(submission);
                }
                else
                {
                    submission.Status = status;
                    submission.SubmittedAt = submittedAt;
                    submission.Notes = notes;
                    submission.UpdatedAt = now;
                }

                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return Result<bool>.Success(true);
            },
            cancellationToken,
            TranslationKeys.Errors.SaveError);

    public Task<Result<IReadOnlyList<HomeworkListItem>>> GetDueForSessionAsync(
        int sessionId,
        CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<HomeworkListItem>>(
            "Homeworks.GetDueForSession",
            async context => await context.Homeworks
                .AsNoTracking()
                .Where(homework => homework.TargetSessionId == sessionId && !homework.IsCompleted)
                .OrderBy(homework => homework.DueDate)
                .ThenBy(homework => homework.Id)
                .Select(homework => new HomeworkListItem(
                    homework.Id,
                    homework.GroupId,
                    homework.SessionId,
                    homework.TargetSessionId,
                    homework.Title,
                    homework.Description,
                    homework.DueDate,
                    homework.IsCompleted,
                    context.HomeworkSubmissions.Count(submission =>
                        submission.HomeworkId == homework.Id),
                    context.HomeworkSubmissions.Count(submission =>
                        submission.HomeworkId == homework.Id
                        && (submission.Status == HomeworkStatuses.Submitted
                            || submission.Status == HomeworkStatuses.Late)),
                    context.HomeworkSubmissions.Count(submission =>
                        submission.HomeworkId == homework.Id
                        && (submission.Status == HomeworkStatuses.Pending
                            || submission.Status == HomeworkStatuses.Missing))))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    public Task<Result<IReadOnlyList<HomeworkListItem>>> GetListAsync(
        HomeworkReportQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var groupId = query.GroupId is > 0 ? query.GroupId : null;
        var fromDate = query.FromDate;
        var toDate = query.ToDate;

        return RunAsync<IReadOnlyList<HomeworkListItem>>(
            "Homeworks.GetList",
            async context =>
            {
                var filtered = context.Homeworks.AsNoTracking().AsQueryable();

                if (groupId is { } groupFilter)
                {
                    filtered = filtered.Where(homework => homework.GroupId == groupFilter);
                }

                if (fromDate is { } start)
                {
                    var rangeStart = new DateTime(
                        start.Year, start.Month, start.Day, 0, 0, 0, DateTimeKind.Utc);
                    filtered = filtered.Where(homework => homework.DueDate >= rangeStart);
                }

                if (toDate is { } end)
                {
                    var rangeEnd = new DateTime(
                        end.Year, end.Month, end.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(1);
                    filtered = filtered.Where(homework => homework.DueDate < rangeEnd);
                }

                return await filtered
                    .OrderByDescending(homework => homework.DueDate)
                    .ThenByDescending(homework => homework.Id)
                    .Select(homework => new HomeworkListItem(
                        homework.Id,
                        homework.GroupId,
                        homework.SessionId,
                        homework.TargetSessionId,
                        homework.Title,
                        homework.Description,
                        homework.DueDate,
                        homework.IsCompleted,
                        context.HomeworkSubmissions.Count(submission =>
                            submission.HomeworkId == homework.Id),
                        context.HomeworkSubmissions.Count(submission =>
                            submission.HomeworkId == homework.Id
                            && (submission.Status == HomeworkStatuses.Submitted
                                || submission.Status == HomeworkStatuses.Late)),
                        context.HomeworkSubmissions.Count(submission =>
                            submission.HomeworkId == homework.Id
                            && (submission.Status == HomeworkStatuses.Pending
                                || submission.Status == HomeworkStatuses.Missing))))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken);
    }

    public Task<Result<HomeworkListItem?>> GetSummaryAsync(
        int homeworkId,
        CancellationToken cancellationToken = default)
        => RunAsync<HomeworkListItem?>(
            "Homeworks.GetSummary",
            async context => await context.Homeworks
                .AsNoTracking()
                .Where(homework => homework.Id == homeworkId)
                .Select(homework => new HomeworkListItem(
                    homework.Id,
                    homework.GroupId,
                    homework.SessionId,
                    homework.TargetSessionId,
                    homework.Title,
                    homework.Description,
                    homework.DueDate,
                    homework.IsCompleted,
                    context.HomeworkSubmissions.Count(submission =>
                        submission.HomeworkId == homework.Id),
                    context.HomeworkSubmissions.Count(submission =>
                        submission.HomeworkId == homework.Id
                        && (submission.Status == HomeworkStatuses.Submitted
                            || submission.Status == HomeworkStatuses.Late)),
                    context.HomeworkSubmissions.Count(submission =>
                        submission.HomeworkId == homework.Id
                        && (submission.Status == HomeworkStatuses.Pending
                            || submission.Status == HomeworkStatuses.Missing))))
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    public Task<Result<IReadOnlyList<HomeworkSubmissionItem>>> GetSubmissionBoardAsync(
        int homeworkId,
        CancellationToken cancellationToken = default)
        => RunResultAsync<IReadOnlyList<HomeworkSubmissionItem>>(
            "Homeworks.GetSubmissionBoard",
            async context =>
            {
                var homework = await context.Homeworks
                    .AsNoTracking()
                    .Where(row => row.Id == homeworkId)
                    .Select(row => new { row.GroupId, row.DueDate })
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (homework is null)
                {
                    return Result<IReadOnlyList<HomeworkSubmissionItem>>.Fail(
                        Failure.NotFound(TranslationKeys.Errors.NotFound));
                }

                // Active students arrive even without a submission row, so the
                // board can show "pending"/"missing" instead of hiding them.
                var rows = await (
                        from enrollment in context.Enrollments.AsNoTracking()
                        join student in context.Students.AsNoTracking()
                            on enrollment.StudentId equals student.Id
                        join submission in context.HomeworkSubmissions.AsNoTracking()
                            on new { StudentId = enrollment.StudentId, HomeworkId = homeworkId }
                            equals new { submission.StudentId, submission.HomeworkId } into submissions
                        from submission in submissions.DefaultIfEmpty()
                        where enrollment.GroupId == homework.GroupId
                            && enrollment.Status == EnrollmentStatuses.Active
                        orderby student.LastName, student.FirstName
                        select new
                        {
                            StudentId = student.Id,
                            student.StudentCode,
                            FirstName = student.FirstName,
                            LastName = student.LastName,
                            SubmissionId = submission == null ? 0 : submission.Id,
                            Status = submission == null ? null : submission.Status,
                            SubmittedAt = submission == null ? null : submission.SubmittedAt,
                            Notes = submission == null ? null : submission.Notes,
                        })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var today = DateTime.SpecifyKind(Clock.LocalNow.Date, DateTimeKind.Utc);
                var items = rows
                    .Select(row => new HomeworkSubmissionItem(
                        row.SubmissionId,
                        homeworkId,
                        row.StudentId,
                        row.StudentCode,
                        row.FirstName + " " + row.LastName,
                        HomeworkPolicy.ResolveExisting(
                            row.Status,
                            row.SubmittedAt,
                            homework.DueDate,
                            today),
                        row.SubmittedAt,
                        row.Notes))
                    .ToList();

                return Result<IReadOnlyList<HomeworkSubmissionItem>>.Success(items);
            },
            cancellationToken);
}
