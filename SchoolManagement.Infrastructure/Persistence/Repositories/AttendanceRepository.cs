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

    private static DateTime StartOfDay(DateTime date)
        => new(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc);
}
