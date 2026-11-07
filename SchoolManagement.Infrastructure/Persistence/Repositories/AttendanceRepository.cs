using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Attendance;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Time;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

public sealed class AttendanceRepository : GenericRepository<AttendanceRecord>, IAttendanceRepository
{
    public AttendanceRepository(
        IDbContextFactory<SchoolManagementDbContext> contextFactory,
        ITrustedClock clock,
        ILogger<AttendanceRepository> logger)
        : base(contextFactory, clock, logger)
    {
    }

    public Task<Result<IReadOnlyList<AttendanceListItem>>> GetBySessionAsync(
        int sessionId,
        CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<AttendanceListItem>>(
            "Attendance.GetBySession",
            async context => await (
                    from record in context.Attendance.AsNoTracking()
                    join student in context.Students.AsNoTracking() on record.StudentId equals student.Id
                    where record.SessionId == sessionId
                    orderby student.LastName, student.FirstName
                    select new AttendanceListItem(
                        record.Id,
                        record.SessionId,
                        record.StudentId,
                        student.StudentCode,
                        student.FirstName + " " + student.LastName,
                        record.Status,
                        record.CheckInTime,
                        record.Notes))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    public Task<Result<IReadOnlyList<AttendanceListItem>>> GetByStudentAsync(
        int studentId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        var rangeStart = StartOfDay(fromDate);
        var rangeEnd = StartOfDay(toDate).AddDays(1);

        return RunAsync<IReadOnlyList<AttendanceListItem>>(
            "Attendance.GetByStudent",
            async context => await (
                    from record in context.Attendance.AsNoTracking()
                    join session in context.Sessions.AsNoTracking() on record.SessionId equals session.Id
                    join student in context.Students.AsNoTracking() on record.StudentId equals student.Id
                    where record.StudentId == studentId
                        && session.SessionDate >= rangeStart
                        && session.SessionDate < rangeEnd
                    orderby session.SessionDate descending, session.StartTime
                    select new AttendanceListItem(
                        record.Id,
                        record.SessionId,
                        record.StudentId,
                        student.StudentCode,
                        student.FirstName + " " + student.LastName,
                        record.Status,
                        record.CheckInTime,
                        record.Notes))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);
    }

    public Task<Result<int>> CountAbsencesAsync(
        int studentId,
        CancellationToken cancellationToken = default)
        => RunAsync(
            "Attendance.CountAbsences",
            async context => await context.Attendance
                .AsNoTracking()
                .CountAsync(
                    record => record.StudentId == studentId
                        && (record.Status == AttendanceStatuses.Absent
                            || record.Status == AttendanceStatuses.Unexcused),
                    cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    public Task<Result<GroupAttendanceStats>> GetGroupStatisticsAsync(
        int groupId,
        CancellationToken cancellationToken = default)
        => RunAsync(
            "Attendance.GetGroupStatistics",
            async context =>
            {
                var sessionCount = await context.Sessions
                    .AsNoTracking()
                    .CountAsync(session => session.GroupId == groupId, cancellationToken)
                    .ConfigureAwait(false);

                var records = context.Attendance
                    .AsNoTracking()
                    .Where(record => context.Sessions
                        .Any(session => session.Id == record.SessionId && session.GroupId == groupId));

                var recordCount = await records.CountAsync(cancellationToken).ConfigureAwait(false);
                var presentCount = await records
                    .CountAsync(record => record.Status == AttendanceStatuses.Present, cancellationToken)
                    .ConfigureAwait(false);
                var lateCount = await records
                    .CountAsync(record => record.Status == AttendanceStatuses.Late, cancellationToken)
                    .ConfigureAwait(false);
                var absentCount = await records
                    .CountAsync(
                        record => record.Status == AttendanceStatuses.Absent
                            || record.Status == AttendanceStatuses.Unexcused,
                        cancellationToken)
                    .ConfigureAwait(false);

                return new GroupAttendanceStats(
                    sessionCount,
                    recordCount,
                    presentCount,
                    absentCount,
                    lateCount);
            },
            cancellationToken);

    public Task<Result<int>> UpsertAsync(
        int sessionId,
        int studentId,
        string status,
        string? notes,
        CancellationToken cancellationToken = default)
        => RunAsync(
            "Attendance.Upsert",
            async context =>
            {
                var now = Clock.LocalNow;
                var record = await context.Attendance
                    .FirstOrDefaultAsync(
                        x => x.SessionId == sessionId && x.StudentId == studentId,
                        cancellationToken)
                    .ConfigureAwait(false);

                var isPresent = string.Equals(status, AttendanceStatuses.Present, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(status, AttendanceStatuses.Late, StringComparison.OrdinalIgnoreCase);

                if (record is null)
                {
                    record = new AttendanceRecord
                    {
                        SessionId = sessionId,
                        StudentId = studentId,
                        Status = status,
                        Notes = notes,
                        CheckInTime = isPresent ? now : null,
                        CreatedAt = now,
                        UpdatedAt = now,
                    };
                    context.Attendance.Add(record);
                }
                else
                {
                    record.Status = status;
                    record.Notes = notes;
                    record.CheckInTime = isPresent ? record.CheckInTime ?? now : null;
                    record.UpdatedAt = now;
                }

                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return record.Id;
            },
            cancellationToken,
            TranslationKeys.Errors.SaveError);

    public Task<Result<IReadOnlyList<AttendanceRosterItem>>> GetRosterAsync(
        int sessionId,
        CancellationToken cancellationToken = default)
        => RunResultAsync<IReadOnlyList<AttendanceRosterItem>>(
            "Attendance.GetRoster",
            async context =>
            {
                var groupId = await context.Sessions
                    .AsNoTracking()
                    .Where(session => session.Id == sessionId)
                    .Select(session => (int?)session.GroupId)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (groupId is null)
                {
                    return Result<IReadOnlyList<AttendanceRosterItem>>.Fail(
                        Failure.NotFound(TranslationKeys.Errors.NotFound));
                }

                // Every active enrollment appears once even before the first
                // save; a missing attendance row is shown as "present", the
                // documented default of the take-attendance screen.
                var rows = await (
                        from enrollment in context.Enrollments.AsNoTracking()
                        join student in context.Students.AsNoTracking()
                            on enrollment.StudentId equals student.Id
                        join record in context.Attendance.AsNoTracking()
                            on new { StudentId = enrollment.StudentId, SessionId = sessionId }
                            equals new { record.StudentId, record.SessionId } into records
                        from record in records.DefaultIfEmpty()
                        where enrollment.GroupId == groupId.Value
                            && enrollment.Status == EnrollmentStatuses.Active
                        orderby student.LastName, student.FirstName
                        select new AttendanceRosterItem(
                            student.Id,
                            student.StudentCode,
                            student.FirstName + " " + student.LastName,
                            record == null ? AttendanceStatuses.Present : record.Status,
                            record == null ? null : record.CheckInTime,
                            record == null ? null : record.Notes))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return Result<IReadOnlyList<AttendanceRosterItem>>.Success(rows);
            },
            cancellationToken);

    public Task<Result<int>> UpsertManyAsync(
        int sessionId,
        IReadOnlyList<AttendanceEntry> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return RunAsync(
            "Attendance.UpsertMany",
            async context =>
            {
                var now = Clock.LocalNow;

                // One read for the whole session: the save never becomes a query
                // per student. The unique (session_id, student_id) key remains
                // the database-level safety net.
                var existing = await context.Attendance
                    .Where(record => record.SessionId == sessionId)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var byStudent = existing.ToDictionary(record => record.StudentId);

                await using var transaction = await context.Database
                    .BeginTransactionAsync(cancellationToken)
                    .ConfigureAwait(false);

                foreach (var entry in entries)
                {
                    var isPresent = string.Equals(
                            entry.Status,
                            AttendanceStatuses.Present,
                            StringComparison.OrdinalIgnoreCase)
                        || string.Equals(
                            entry.Status,
                            AttendanceStatuses.Late,
                            StringComparison.OrdinalIgnoreCase);

                    if (byStudent.TryGetValue(entry.StudentId, out var record))
                    {
                        record.Status = entry.Status;
                        record.Notes = entry.Notes;
                        record.CheckInTime = isPresent ? record.CheckInTime ?? now : null;
                        record.UpdatedAt = now;
                    }
                    else
                    {
                        record = new AttendanceRecord
                        {
                            SessionId = sessionId,
                            StudentId = entry.StudentId,
                            Status = entry.Status,
                            Notes = entry.Notes,
                            CheckInTime = isPresent ? now : null,
                            CreatedAt = now,
                            UpdatedAt = now,
                        };
                        context.Attendance.Add(record);
                        byStudent[entry.StudentId] = record;
                    }
                }

                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return entries.Count;
            },
            cancellationToken,
            TranslationKeys.Errors.SaveError);
    }

    public Task<Result<IReadOnlyList<AttendanceReportRow>>> GetReportAsync(
        AttendanceReportQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rangeStart = StartOfDay(query.FromDate);
        var rangeEnd = StartOfDay(query.ToDate).AddDays(1);
        var studentId = query.StudentId is > 0 ? query.StudentId : null;
        var groupId = query.GroupId is > 0 ? query.GroupId : null;

        return RunAsync<IReadOnlyList<AttendanceReportRow>>(
            "Attendance.GetReport",
            async context =>
            {
                var filtered = from record in context.Attendance.AsNoTracking()
                    join session in context.Sessions.AsNoTracking()
                        on record.SessionId equals session.Id
                    where session.SessionDate >= rangeStart
                        && session.SessionDate < rangeEnd
                    select new { record, session };

                if (studentId is { } studentFilter)
                {
                    filtered = filtered.Where(row => row.record.StudentId == studentFilter);
                }

                if (groupId is { } groupFilter)
                {
                    filtered = filtered.Where(row => row.session.GroupId == groupFilter);
                }

                // One grouped query per student and group; the conditional
                // counts run in SQL, never in memory.
                return await (
                        from row in filtered
                        join student in context.Students.AsNoTracking()
                            on row.record.StudentId equals student.Id
                        join groupEntity in context.Groups.AsNoTracking()
                            on row.session.GroupId equals groupEntity.Id
                        group new { row.record, student, groupEntity }
                        by new
                        {
                            row.record.StudentId,
                            student.StudentCode,
                            student.FirstName,
                            student.LastName,
                            row.session.GroupId,
                            groupEntity.Name,
                        }
                        into grouped
                        orderby grouped.Key.LastName, grouped.Key.FirstName
                        select new AttendanceReportRow(
                            grouped.Key.StudentId,
                            grouped.Key.StudentCode,
                            grouped.Key.FirstName + " " + grouped.Key.LastName,
                            grouped.Key.GroupId,
                            grouped.Key.Name,
                            grouped.Count(),
                            grouped.Count(item => item.record.Status == AttendanceStatuses.Present),
                            grouped.Count(item => item.record.Status == AttendanceStatuses.Absent
                                || item.record.Status == AttendanceStatuses.Unexcused),
                            grouped.Count(item => item.record.Status == AttendanceStatuses.Late)))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken);
    }

    private static DateTime StartOfDay(DateTime date)
        => new(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc);
}
