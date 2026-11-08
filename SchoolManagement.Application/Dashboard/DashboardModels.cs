using SchoolManagement.Domain.Policies;

namespace SchoolManagement.Application.Dashboard;

// Phase D4 - read-only dashboard aggregates. Every record here is a projection
// shaped for one widget of the dashboard; nothing is written back and no schema
// rule changes.

/// <summary>One session of the selected day, with its roster and attendance counts.</summary>
public sealed record DashboardSessionItem(
    int Id,
    int GroupId,
    string GroupName,
    DateTime StartTime,
    DateTime EndTime,
    string Status,
    string Type,
    int StudentCount,
    int AttendanceCount)
{
    public bool HasAttendance => AttendanceCount > 0;

    /// <summary>The single read-only gate of the roster screen.</summary>
    public bool CanTakeAttendance => SessionStatusPolicy.CanTakeAttendance(Status);
}

/// <summary>A student at or above the configured absence limit.</summary>
public sealed record DashboardAbsenceAlert(
    int StudentId,
    string StudentCode,
    string StudentName,
    int AbsenceCount,
    int Limit);

/// <summary>One month of the six-month revenue trend. The key is "YYYY-MM".</summary>
public sealed record DashboardMonthPoint(string MonthYear, double Revenue);

/// <summary>Attendance counters of one school day (0 = Saturday ... 6 = Friday).</summary>
public sealed record DashboardDayPoint(int DayOfWeek, int Present, int Absent, int Late)
{
    public int Total => Present + Absent + Late;

    public double AttendanceRate => Total == 0
        ? 0
        : (Present + Late) * 100d / Total;
}

/// <summary>Active students of one academic level. An unplaced student has a null name.</summary>
public sealed record DashboardLevelSlice(string? LevelName, int StudentCount);

/// <summary>One step of the new-school guide; the key is a translation key.</summary>
public sealed record DashboardSetupStep(string Key, int Count, bool IsComplete);

/// <summary>Everything the dashboard renders, loaded in one use-case call.</summary>
public sealed record DashboardSnapshot(
    string SchoolName,
    DateTime Today,
    string MonthYear,
    int ActiveStudents,
    int ActiveGroups,
    int TodaySessionCount,
    double MonthRevenue,
    double MonthOutstanding,
    int ArrearsStudentCount,
    IReadOnlyList<DashboardSessionItem> Sessions,
    IReadOnlyList<DashboardAbsenceAlert> AbsenceAlerts,
    IReadOnlyList<DashboardMonthPoint> RevenueTrend,
    IReadOnlyList<DashboardDayPoint> WeekAttendance,
    IReadOnlyList<DashboardLevelSlice> LevelDistribution,
    IReadOnlyList<DashboardSetupStep> SetupSteps)
{
    /// <summary>A school with no group and no student shows the guided start screen.</summary>
    public bool IsNewSchool => ActiveGroups == 0 && ActiveStudents == 0;
}
