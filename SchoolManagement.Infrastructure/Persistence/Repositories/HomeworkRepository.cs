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
}
