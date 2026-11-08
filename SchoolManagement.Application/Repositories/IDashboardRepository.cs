using SchoolManagement.Application.Common;
using SchoolManagement.Application.Dashboard;

namespace SchoolManagement.Application.Repositories;

/// <summary>
/// Phase D4 - read-only aggregate queries used only by the dashboard. The
/// dashboard needs counters and grouped totals that no existing repository
/// exposes without loading whole tables, so this is the documented read-only
/// exception to the "no new persistence for a design phase" rule. It has no
/// write method and the schema is untouched.
/// </summary>
public interface IDashboardRepository
{
    /// <summary>Sessions of one calendar day joined with their group name and counts.</summary>
    Task<Result<IReadOnlyList<DashboardSessionItem>>> GetTodaySessionsAsync(
        DateTime today,
        CancellationToken cancellationToken = default);

    /// <summary>Active students whose absence count reached the configured limit.</summary>
    Task<Result<IReadOnlyList<DashboardAbsenceAlert>>> GetAbsenceAlertsAsync(
        int limit,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>Monthly payments grouped by "YYYY-MM" for the requested months.</summary>
    Task<Result<IReadOnlyList<DashboardMonthPoint>>> GetRevenueTrendAsync(
        IReadOnlyList<string> months,
        CancellationToken cancellationToken = default);

    /// <summary>Present/absent/late counters for the seven days starting at <paramref name="weekStart"/>.</summary>
    Task<Result<IReadOnlyList<DashboardDayPoint>>> GetWeekAttendanceAsync(
        DateTime weekStart,
        CancellationToken cancellationToken = default);

    /// <summary>Active students grouped by the academic level of their active group.</summary>
    Task<Result<IReadOnlyList<DashboardLevelSlice>>> GetLevelDistributionAsync(
        CancellationToken cancellationToken = default);
}
