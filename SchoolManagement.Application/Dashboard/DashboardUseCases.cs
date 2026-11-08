using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Students;
using SchoolManagement.Application.Time;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Policies;

namespace SchoolManagement.Application.Dashboard;

/// <summary>
/// Phase D4 - one read-only call that assembles the dashboard snapshot.
///
/// It composes the existing repositories (students, groups, monthly dues,
/// reference data, settings) with the new read-only aggregate repository, so
/// the dashboard never re-implements the finance, absence or session rules.
/// It writes nothing and therefore carries no [WriteOperation] and no license
/// guard, exactly like the other read use cases.
/// </summary>
public sealed class GetDashboardSnapshotUseCase
{
    private const int AbsenceAlertTake = 5;
    private const int RevenueTrendMonths = 6;

    private readonly IDashboardRepository _dashboard;
    private readonly IStudentRepository _students;
    private readonly IGroupRepository _groups;
    private readonly IPaymentRepository _payments;
    private readonly IReferenceDataRepository _reference;
    private readonly ISettingsRepository _settings;
    private readonly ITrustedClock _clock;

    public GetDashboardSnapshotUseCase(
        IDashboardRepository dashboard,
        IStudentRepository students,
        IGroupRepository groups,
        IPaymentRepository payments,
        IReferenceDataRepository reference,
        ISettingsRepository settings,
        ITrustedClock clock)
    {
        _dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));
        _students = students ?? throw new ArgumentNullException(nameof(students));
        _groups = groups ?? throw new ArgumentNullException(nameof(groups));
        _payments = payments ?? throw new ArgumentNullException(nameof(payments));
        _reference = reference ?? throw new ArgumentNullException(nameof(reference));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<DashboardSnapshot>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.SpecifyKind(_clock.LocalNow.Date, DateTimeKind.Utc);
        var monthYear = MonthlyDuesCalculator.ToMonthYear(today);
        var months = LastMonths(today, RevenueTrendMonths);
        var weekStart = StartOfSchoolWeek(today);

        var school = await _reference.GetSchoolNameAsync(cancellationToken).ConfigureAwait(false);
        if (school.IsFailure)
        {
            return Result<DashboardSnapshot>.Fail(school.Failure!);
        }

        var activeStudents = await _students
            .CountAsync(new StudentListQuery(Status: StudentStatuses.Active), cancellationToken)
            .ConfigureAwait(false);
        if (activeStudents.IsFailure)
        {
            return Result<DashboardSnapshot>.Fail(activeStudents.Failure!);
        }

        var groups = await _groups.GetSummariesAsync(false, cancellationToken).ConfigureAwait(false);
        if (groups.IsFailure)
        {
            return Result<DashboardSnapshot>.Fail(groups.Failure!);
        }

        var dues = await _payments.GetMonthlyDuesAsync(monthYear, null, cancellationToken).ConfigureAwait(false);
        if (dues.IsFailure)
        {
            return Result<DashboardSnapshot>.Fail(dues.Failure!);
        }

        var levels = await _reference.GetAcademicLevelsAsync(true, cancellationToken).ConfigureAwait(false);
        if (levels.IsFailure)
        {
            return Result<DashboardSnapshot>.Fail(levels.Failure!);
        }

        var subjects = await _reference.GetSubjectsAsync(true, cancellationToken).ConfigureAwait(false);
        if (subjects.IsFailure)
        {
            return Result<DashboardSnapshot>.Fail(subjects.Failure!);
        }

        var teachers = await _reference.GetTeachersAsync(true, cancellationToken).ConfigureAwait(false);
        if (teachers.IsFailure)
        {
            return Result<DashboardSnapshot>.Fail(teachers.Failure!);
        }

        var absenceLimit = await _settings.GetAbsenceLimitAsync(cancellationToken).ConfigureAwait(false);
        if (absenceLimit.IsFailure)
        {
            return Result<DashboardSnapshot>.Fail(absenceLimit.Failure!);
        }

        var sessions = await _dashboard.GetTodaySessionsAsync(today, cancellationToken).ConfigureAwait(false);
        if (sessions.IsFailure)
        {
            return Result<DashboardSnapshot>.Fail(sessions.Failure!);
        }

        var absences = await _dashboard
            .GetAbsenceAlertsAsync(absenceLimit.Value, AbsenceAlertTake, cancellationToken)
            .ConfigureAwait(false);
        if (absences.IsFailure)
        {
            return Result<DashboardSnapshot>.Fail(absences.Failure!);
        }

        var revenue = await _dashboard.GetRevenueTrendAsync(months, cancellationToken).ConfigureAwait(false);
        if (revenue.IsFailure)
        {
            return Result<DashboardSnapshot>.Fail(revenue.Failure!);
        }

        var week = await _dashboard.GetWeekAttendanceAsync(weekStart, cancellationToken).ConfigureAwait(false);
        if (week.IsFailure)
        {
            return Result<DashboardSnapshot>.Fail(week.Failure!);
        }

        var distribution = await _dashboard.GetLevelDistributionAsync(cancellationToken).ConfigureAwait(false);
        if (distribution.IsFailure)
        {
            return Result<DashboardSnapshot>.Fail(distribution.Failure!);
        }

        var studentCount = activeStudents.Value;
        var groupCount = groups.Value.Count;
        var setupSteps = new List<DashboardSetupStep>
        {
            new(TranslationKeys.Dashboard.SetupLevels, levels.Value.Count, levels.Value.Count > 0),
            new(TranslationKeys.Dashboard.SetupSubjects, subjects.Value.Count, subjects.Value.Count > 0),
            new(TranslationKeys.Dashboard.SetupTeachers, teachers.Value.Count, teachers.Value.Count > 0),
            new(TranslationKeys.Dashboard.SetupGroups, groupCount, groupCount > 0),
            new(TranslationKeys.Dashboard.SetupStudents, studentCount, studentCount > 0),
        };

        return Result<DashboardSnapshot>.Success(new DashboardSnapshot(
            school.Value,
            today,
            monthYear,
            studentCount,
            groupCount,
            sessions.Value.Count,
            dues.Value.Sum(due => due.Paid),
            dues.Value.Sum(due => due.Outstanding),
            dues.Value.Where(due => due.Outstanding > 0)
                .Select(due => due.StudentId)
                .Distinct()
                .Count(),
            sessions.Value,
            absences.Value,
            FillRevenueTrend(months, revenue.Value),
            FillWeek(weekStart, week.Value),
            distribution.Value,
            setupSteps));
    }

    /// <summary>The six months ending with the current one, oldest first.</summary>
    private static List<string> LastMonths(DateTime today, int count)
    {
        var months = new List<string>(count);
        var first = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddMonths(-(count - 1));

        for (var index = 0; index < count; index++)
        {
            months.Add(MonthlyDuesCalculator.ToMonthYear(first.AddMonths(index)));
        }

        return months;
    }

    /// <summary>Saturday is the first day of the school week.</summary>
    private static DateTime StartOfSchoolWeek(DateTime today)
        => today.AddDays(-(int)WeekDayCalculator.FromDate(today));

    private static IReadOnlyList<DashboardMonthPoint> FillRevenueTrend(
        IReadOnlyList<string> months,
        IReadOnlyList<DashboardMonthPoint> loaded)
    {
        var byMonth = loaded
            .GroupBy(point => point.MonthYear, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Sum(point => point.Revenue), StringComparer.Ordinal);

        return months
            .Select(month => new DashboardMonthPoint(
                month,
                byMonth.TryGetValue(month, out var revenue) ? revenue : 0))
            .ToList();
    }

    private static IReadOnlyList<DashboardDayPoint> FillWeek(
        DateTime weekStart,
        IReadOnlyList<DashboardDayPoint> loaded)
    {
        var byDay = loaded.ToDictionary(point => point.DayOfWeek);
        var days = new List<DashboardDayPoint>(7);

        for (var offset = 0; offset < 7; offset++)
        {
            var day = (int)WeekDayCalculator.FromDate(weekStart.AddDays(offset));
            days.Add(byDay.TryGetValue(day, out var point)
                ? point
                : new DashboardDayPoint(day, 0, 0, 0));
        }

        return days;
    }
}
