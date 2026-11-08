using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Dashboard;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Time;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>
/// Phase D4 - read-only dashboard aggregates. Every method is a single grouped
/// or counted query: the dashboard never loads a whole table and never runs a
/// query per row. There is no write method and the schema is untouched.
/// </summary>
public sealed class DashboardRepository : RepositoryBase, IDashboardRepository
{
    public DashboardRepository(
        IDbContextFactory<SchoolManagementDbContext> contextFactory,
        ITrustedClock clock,
        ILogger<DashboardRepository> logger)
        : base(contextFactory, clock, logger)
    {
    }

    public Task<Result<IReadOnlyList<DashboardSessionItem>>> GetTodaySessionsAsync(
        DateTime today,
        CancellationToken cancellationToken = default)
    {
        var start = StartOfDay(today);
        var end = start.AddDays(1);

        return RunAsync<IReadOnlyList<DashboardSessionItem>>(
            "Dashboard.TodaySessions",
            async context => await (
                    from session in context.Sessions.AsNoTracking()
                    join groupRow in context.Groups.AsNoTracking()
                        on session.GroupId equals groupRow.Id
                    where session.SessionDate >= start && session.SessionDate < end
                    orderby session.StartTime
                    select new DashboardSessionItem(
                        session.Id,
                        session.GroupId,
                        groupRow.Name,
                        session.StartTime,
                        session.EndTime,
                        session.Status,
                        session.Type,
                        context.Enrollments.Count(enrollment =>
                            enrollment.GroupId == session.GroupId
                            && enrollment.Status == EnrollmentStatuses.Active),
                        context.Attendance.Count(record => record.SessionId == session.Id)))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);
    }

    public Task<Result<IReadOnlyList<DashboardAbsenceAlert>>> GetAbsenceAlertsAsync(
        int limit,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            return Task.FromResult(Result<IReadOnlyList<DashboardAbsenceAlert>>.Success(
                Array.Empty<DashboardAbsenceAlert>()));
        }

        return RunAsync<IReadOnlyList<DashboardAbsenceAlert>>(
            "Dashboard.AbsenceAlerts",
            async context =>
            {
                // Group the absence rows once, then join the students. This is
                // cheaper than repeating a correlated COUNT in WHERE and ORDER BY.
                var absenceCounts = context.Attendance.AsNoTracking()
                    .Where(record => record.Status == AttendanceStatuses.Absent
                        || record.Status == AttendanceStatuses.Unexcused)
                    .GroupBy(record => record.StudentId)
                    .Select(grouped => new
                    {
                        StudentId = grouped.Key,
                        Count = grouped.Count(),
                    });

                return await (
                        from student in context.Students.AsNoTracking()
                        join absence in absenceCounts on student.Id equals absence.StudentId
                        where student.Status == StudentStatuses.Active && absence.Count >= limit
                        orderby absence.Count descending, student.LastName, student.FirstName
                        select new DashboardAbsenceAlert(
                            student.Id,
                            student.StudentCode,
                            student.FirstName + " " + student.LastName,
                            absence.Count,
                            limit))
                    .Take(take)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken);
    }

    public Task<Result<IReadOnlyList<DashboardMonthPoint>>> GetRevenueTrendAsync(
        IReadOnlyList<string> months,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(months);

        // List<T>.Contains is an instance method, so the expression never binds
        // to MemoryExtensions.Contains(ReadOnlySpan<T>) which EF cannot compile.
        var monthList = months as List<string> ?? months.ToList();

        return RunAsync<IReadOnlyList<DashboardMonthPoint>>(
            "Dashboard.RevenueTrend",
            async context =>
            {
                var rows = await context.Payments.AsNoTracking()
                    .Where(payment => payment.MonthYear != null && monthList.Contains(payment.MonthYear))
                    .GroupBy(payment => payment.MonthYear!)
                    .Select(grouped => new
                    {
                        MonthYear = grouped.Key,
                        Revenue = grouped.Sum(payment => payment.Amount),
                    })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return rows
                    .Select(row => new DashboardMonthPoint(row.MonthYear, row.Revenue))
                    .ToList();
            },
            cancellationToken);
    }

    public Task<Result<IReadOnlyList<DashboardDayPoint>>> GetWeekAttendanceAsync(
        DateTime weekStart,
        CancellationToken cancellationToken = default)
    {
        var start = StartOfDay(weekStart);
        var end = start.AddDays(7);

        return RunAsync<IReadOnlyList<DashboardDayPoint>>(
            "Dashboard.WeekAttendance",
            async context =>
            {
                var rows = await (
                        from record in context.Attendance.AsNoTracking()
                        join session in context.Sessions.AsNoTracking()
                            on record.SessionId equals session.Id
                        where session.SessionDate >= start && session.SessionDate < end
                        group record by session.SessionDate
                        into grouped
                        select new
                        {
                            Date = grouped.Key,
                            Present = grouped.Count(item => item.Status == AttendanceStatuses.Present),
                            Absent = grouped.Count(item => item.Status == AttendanceStatuses.Absent
                                || item.Status == AttendanceStatuses.Unexcused),
                            Late = grouped.Count(item => item.Status == AttendanceStatuses.Late),
                        })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                // SQLite cannot translate DayOfWeek; the seven rows are converted
                // in memory, which is a fixed seven-item projection.
                return rows
                    .Select(row => new DashboardDayPoint(
                        (int)WeekDayCalculator.FromDate(row.Date),
                        row.Present,
                        row.Absent,
                        row.Late))
                    .ToList();
            },
            cancellationToken);
    }

    public Task<Result<IReadOnlyList<DashboardLevelSlice>>> GetLevelDistributionAsync(
        CancellationToken cancellationToken = default)
    {
        return RunAsync<IReadOnlyList<DashboardLevelSlice>>(
            "Dashboard.LevelDistribution",
            async context =>
            {
                // One row per (active student, level) pair, then grouped in
                // memory so a student enrolled in two groups of the same level
                // is counted once per level.
                var rows = await (
                        from enrollment in context.Enrollments.AsNoTracking()
                        join groupRow in context.Groups.AsNoTracking()
                            on enrollment.GroupId equals groupRow.Id
                        join level in context.AcademicLevels.AsNoTracking()
                            on groupRow.AcademicLevelId equals level.Id into levels
                        from level in levels.DefaultIfEmpty()
                        where enrollment.Status == EnrollmentStatuses.Active
                        select new
                        {
                            enrollment.StudentId,
                            LevelName = level == null ? null : level.Name,
                        })
                    .Distinct()
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return rows
                    .GroupBy(row => row.LevelName, StringComparer.Ordinal)
                    .Select(grouped => new DashboardLevelSlice(grouped.Key, grouped.Count()))
                    .OrderByDescending(slice => slice.StudentCount)
                    .ThenBy(slice => slice.LevelName, StringComparer.Ordinal)
                    .ToList();
            },
            cancellationToken);
    }

    private static DateTime StartOfDay(DateTime date)
        => new(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc);
}
