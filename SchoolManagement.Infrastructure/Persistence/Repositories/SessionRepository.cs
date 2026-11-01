using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Time;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

public sealed class SessionRepository : GenericRepository<Session>, ISessionRepository
{
    public SessionRepository(
        IDbContextFactory<SchoolManagementDbContext> contextFactory,
        ITrustedClock clock,
        ILogger<SessionRepository> logger)
        : base(contextFactory, clock, logger)
    {
    }

    public Task<Result<IReadOnlyList<SessionListItem>>> GetByDateAsync(
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        var dayStart = StartOfDay(date);
        var dayEnd = dayStart.AddDays(1);

        return RunAsync<IReadOnlyList<SessionListItem>>(
            "Sessions.GetByDate",
            async context => await (
                    from session in context.Sessions.AsNoTracking()
                    join groupEntity in context.Groups.AsNoTracking() on session.GroupId equals groupEntity.Id
                    where session.SessionDate >= dayStart && session.SessionDate < dayEnd
                    orderby session.StartTime
                    select new SessionListItem(
                        session.Id,
                        session.GroupId,
                        groupEntity.Name,
                        session.SessionDate,
                        session.StartTime,
                        session.EndTime,
                        session.Type,
                        session.Status,
                        session.LessonTopic,
                        session.IsPaid,
                        session.Price,
                        session.PaymentStatus,
                        session.PaidAmount,
                        session.RemainingAmount))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);
    }

    public Task<Result<IReadOnlyList<SessionListItem>>> GetByGroupAsync(
        int groupId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        var rangeStart = StartOfDay(fromDate);
        var rangeEnd = StartOfDay(toDate).AddDays(1);

        return RunAsync<IReadOnlyList<SessionListItem>>(
            "Sessions.GetByGroup",
            async context => await (
                    from session in context.Sessions.AsNoTracking()
                    join groupEntity in context.Groups.AsNoTracking() on session.GroupId equals groupEntity.Id
                    where session.GroupId == groupId
                        && session.SessionDate >= rangeStart
                        && session.SessionDate < rangeEnd
                    orderby session.SessionDate, session.StartTime
                    select new SessionListItem(
                        session.Id,
                        session.GroupId,
                        groupEntity.Name,
                        session.SessionDate,
                        session.StartTime,
                        session.EndTime,
                        session.Type,
                        session.Status,
                        session.LessonTopic,
                        session.IsPaid,
                        session.Price,
                        session.PaymentStatus,
                        session.PaidAmount,
                        session.RemainingAmount))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);
    }

    public Task<Result<bool>> ExistsAsync(
        int groupId,
        DateTime sessionDate,
        DateTime startTime,
        CancellationToken cancellationToken = default)
    {
        var dayStart = StartOfDay(sessionDate);
        var dayEnd = dayStart.AddDays(1);

        return RunAsync(
            "Sessions.Exists",
            async context => await context.Sessions
                .AsNoTracking()
                .AnyAsync(
                    session => session.GroupId == groupId
                        && session.SessionDate >= dayStart
                        && session.SessionDate < dayEnd
                        && session.StartTime == startTime,
                    cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);
    }

    public Task<Result<bool>> SetStatusAsync(
        int sessionId,
        string status,
        string? cancelReason,
        CancellationToken cancellationToken = default)
        => RunResultAsync(
            "Sessions.SetStatus",
            async context =>
            {
                var session = await context.Sessions
                    .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken)
                    .ConfigureAwait(false);

                if (session is null)
                {
                    return Result<bool>.Fail(Failure.NotFound(TranslationKeys.Errors.NotFound));
                }

                session.Status = status;
                session.CancelReason = cancelReason;
                session.UpdatedAt = Clock.LocalNow;
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return Result<bool>.Success(true);
            },
            cancellationToken,
            TranslationKeys.Errors.SaveError);

    private static DateTime StartOfDay(DateTime date)
        => new(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc);
}
