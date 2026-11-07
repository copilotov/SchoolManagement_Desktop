using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Attendance;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Groups;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Time;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Policies;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

public sealed class SessionRepository : GenericRepository<Session>, ISessionRepository
{
    // Session generation is an "insert if missing" operation. SQLite has no
    // partial update/insert with a returned row, so a static gate serializes
    // concurrent callers before the read/insert pair runs inside a transaction.
    private static readonly SemaphoreSlim GenerationGate = new(1, 1);

    // Extra/makeup sessions use the same insert-if-missing shape, with their own
    // gate so an extra session never blocks the daily generation.
    private static readonly SemaphoreSlim ExtraSessionGate = new(1, 1);

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
                        session.RemainingAmount,
                        session.CancelReason))
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
                        session.RemainingAmount,
                        session.CancelReason))
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

    public async Task<Result<SessionGenerationResult>> GenerateForDateAsync(
        DateTime date,
        int? groupId = null,
        CancellationToken cancellationToken = default)
    {
        var day = WeekDayCalculator.FromDate(date);
        var dayIndex = WeekDayCalculator.ToIndex(day);
        var dayStart = StartOfDay(date);

        await GenerationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await RunAsync(
                "Sessions.GenerateForDate",
                async context =>
                {
                    var templatesQuery = context.ScheduleTemplates
                        .AsNoTracking()
                        .Where(template => template.IsActive && template.DayOfWeek == dayIndex);
                    if (groupId is { } filter)
                    {
                        templatesQuery = templatesQuery.Where(template => template.GroupId == filter);
                    }

                    var templates = await templatesQuery
                        .OrderBy(template => template.StartTime)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);

                    if (templates.Count == 0)
                    {
                        return new SessionGenerationResult(0, 0);
                    }

                    // Existing sessions of that day are loaded once, so the
                    // existence check never becomes a query per template.
                    var existing = await context.Sessions
                        .AsNoTracking()
                        .Where(session => session.SessionDate >= dayStart
                            && session.SessionDate < dayStart.AddDays(1))
                        .Select(session => new { session.GroupId, session.StartTime })
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);

                    var known = existing
                        .Select(row => (row.GroupId, row.StartTime))
                        .ToHashSet();

                    var now = Clock.LocalNow;
                    var created = 0;
                    var alreadyExisted = 0;

                    await using var transaction = await context.Database
                        .BeginTransactionAsync(cancellationToken)
                        .ConfigureAwait(false);

                    foreach (var template in templates)
                    {
                        var start = SessionSchedulePolicy.Combine(date, template.StartTime.TimeOfDay);
                        var key = (template.GroupId, start);
                        if (known.Contains(key))
                        {
                            alreadyExisted++;
                            continue;
                        }

                        context.Sessions.Add(new Session
                        {
                            GroupId = template.GroupId,
                            TemplateId = template.Id,
                            SessionDate = dayStart,
                            StartTime = start,
                            EndTime = SessionSchedulePolicy.Combine(date, template.EndTime.TimeOfDay),
                            Type = SessionTypes.Regular,
                            Status = SessionStatuses.Scheduled,
                            CreatedAt = now,
                            UpdatedAt = now,
                        });
                        known.Add(key);
                        created++;
                    }

                    if (created > 0)
                    {
                        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    }

                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    return new SessionGenerationResult(created, alreadyExisted);
                },
                cancellationToken,
                TranslationKeys.Errors.SaveError).ConfigureAwait(false);
        }
        finally
        {
            GenerationGate.Release();
        }
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

    public Task<Result<IReadOnlyList<SessionListItem>>> GetListAsync(
        SessionListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Filters and orderings stay on entity columns; the join and projection
        // only run afterwards because EF cannot translate predicates over a
        // projection that carries joined names.
        var fromDate = query.FromDate;
        var toDate = query.ToDate;
        var groupId = query.GroupId;
        var status = string.IsNullOrWhiteSpace(query.Status) ? null : query.Status.Trim();
        var type = string.IsNullOrWhiteSpace(query.Type) ? null : query.Type.Trim();

        return RunAsync<IReadOnlyList<SessionListItem>>(
            "Sessions.GetList",
            async context =>
            {
                var filtered = context.Sessions.AsNoTracking().AsQueryable();

                if (fromDate is { } start)
                {
                    var rangeStart = StartOfDay(start);
                    filtered = filtered.Where(session => session.SessionDate >= rangeStart);
                }

                if (toDate is { } end)
                {
                    var rangeEnd = StartOfDay(end).AddDays(1);
                    filtered = filtered.Where(session => session.SessionDate < rangeEnd);
                }

                if (groupId is > 0)
                {
                    filtered = filtered.Where(session => session.GroupId == groupId);
                }

                if (status is not null)
                {
                    filtered = filtered.Where(session => session.Status == status);
                }

                if (type is not null)
                {
                    filtered = filtered.Where(session => session.Type == type);
                }

                return await (
                        from session in filtered
                        join groupEntity in context.Groups.AsNoTracking()
                            on session.GroupId equals groupEntity.Id
                        orderby session.SessionDate descending, session.StartTime
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
                            session.RemainingAmount,
                            session.CancelReason))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken);
    }

    public Task<Result<SessionListItem?>> GetSummaryAsync(
        int sessionId,
        CancellationToken cancellationToken = default)
        => RunAsync<SessionListItem?>(
            "Sessions.GetSummary",
            async context => await (
                    from session in context.Sessions.AsNoTracking()
                    join groupEntity in context.Groups.AsNoTracking()
                        on session.GroupId equals groupEntity.Id
                    where session.Id == sessionId
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
                        session.RemainingAmount,
                        session.CancelReason))
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    public async Task<Result<ExtraSessionOutcome>> CreateExtraAsync(
        ExtraSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var dayStart = StartOfDay(request.SessionDate);
        var start = SessionSchedulePolicy.Combine(dayStart, request.StartTime);
        var end = SessionSchedulePolicy.Combine(dayStart, request.EndTime);

        await ExtraSessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await RunResultAsync(
                "Sessions.CreateExtra",
                async context =>
                {
                    var existingId = await context.Sessions
                        .AsNoTracking()
                        .Where(session => session.GroupId == request.GroupId
                            && session.SessionDate >= dayStart
                            && session.SessionDate < dayStart.AddDays(1)
                            && session.StartTime == start)
                        .Select(session => (int?)session.Id)
                        .FirstOrDefaultAsync(cancellationToken)
                        .ConfigureAwait(false);

                    if (existingId is { } id)
                    {
                        // The caller asked for a slot that already exists. It is
                        // reported as "not created" instead of an error so a
                        // double click is harmless.
                        return Result<ExtraSessionOutcome>.Success(
                            new ExtraSessionOutcome(id, false));
                    }

                    var now = Clock.LocalNow;
                    var type = string.Equals(request.Type, SessionTypes.Makeup, StringComparison.OrdinalIgnoreCase)
                        ? SessionTypes.Makeup
                        : SessionTypes.Extra;
                    var price = request.Price is > 0 ? request.Price : null;

                    var session = new Session
                    {
                        GroupId = request.GroupId,
                        TemplateId = null,
                        SessionDate = dayStart,
                        StartTime = start,
                        EndTime = end,
                        Type = type,
                        Status = SessionStatuses.Scheduled,
                        LessonTopic = string.IsNullOrWhiteSpace(request.LessonTopic)
                            ? null
                            : request.LessonTopic.Trim(),
                        TeacherNotes = string.IsNullOrWhiteSpace(request.TeacherNotes)
                            ? null
                            : request.TeacherNotes.Trim(),
                        IsPaid = false,
                        Price = price,
                        PaymentStatus = price is null ? null : PaymentStatuses.Unpaid,
                        PaidAmount = 0,
                        RemainingAmount = price ?? 0,
                        CreatedAt = now,
                        UpdatedAt = now,
                    };

                    await using var transaction = await context.Database
                        .BeginTransactionAsync(cancellationToken)
                        .ConfigureAwait(false);
                    context.Sessions.Add(session);
                    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                    return Result<ExtraSessionOutcome>.Success(
                        new ExtraSessionOutcome(session.Id, true));
                },
                cancellationToken,
                TranslationKeys.Errors.SaveError).ConfigureAwait(false);
        }
        finally
        {
            ExtraSessionGate.Release();
        }
    }

    public Task<Result<bool>> UpdatePaymentAsync(
        int sessionId,
        bool isPaid,
        string? paymentStatus,
        double paidAmount,
        double remainingAmount,
        CancellationToken cancellationToken = default)
        => RunResultAsync(
            "Sessions.UpdatePayment",
            async context =>
            {
                var session = await context.Sessions
                    .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken)
                    .ConfigureAwait(false);

                if (session is null)
                {
                    return Result<bool>.Fail(Failure.NotFound(TranslationKeys.Errors.NotFound));
                }

                session.IsPaid = isPaid;
                session.PaymentStatus = paymentStatus;
                session.PaidAmount = paidAmount;
                session.RemainingAmount = Math.Max(0, remainingAmount);
                session.UpdatedAt = Clock.LocalNow;
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return Result<bool>.Success(true);
            },
            cancellationToken,
            TranslationKeys.Errors.SaveError);

    public Task<Result<IReadOnlyList<GuardianContact>>> GetGuardianContactsAsync(
        int groupId,
        CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<GuardianContact>>(
            "Sessions.GetGuardianContacts",
            async context =>
            {
                // A guardian row is optional (students.guardian_id is nullable),
                // so the join is a left join and the phone filter runs in memory.
                var rows = await (
                        from enrollment in context.Enrollments.AsNoTracking()
                        join student in context.Students.AsNoTracking()
                            on enrollment.StudentId equals student.Id
                        join guardian in context.Guardians.AsNoTracking()
                            on student.GuardianId equals (int?)guardian.Id into guardians
                        from guardian in guardians.DefaultIfEmpty()
                        where enrollment.GroupId == groupId
                            && enrollment.Status == EnrollmentStatuses.Active
                        orderby student.LastName, student.FirstName
                        select new GuardianContact(
                            student.Id,
                            student.FirstName + " " + student.LastName,
                            guardian == null ? null : guardian.FullName,
                            guardian == null ? null : guardian.Phone))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return rows
                    .Where(row => !string.IsNullOrWhiteSpace(row.Phone))
                    .ToList();
            },
            cancellationToken);

    private static DateTime StartOfDay(DateTime date)
        => new(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc);
}
