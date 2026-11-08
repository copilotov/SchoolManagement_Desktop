using SchoolManagement.Desktop.Controls;

namespace SchoolManagement.Desktop.ViewModels;

// Phase D4 - display records and the responsive column helper of the dashboard.
// Every record is already localized by the ViewModel, so the view binds text and
// never re-derives a rule or a translation.

/// <summary>Screen a stat card navigates to when it is clicked.</summary>
public enum DashboardStatTarget
{
    Students,
    Groups,
    Sessions,
    Revenue,
    Arrears,
}

/// <summary>Screen an attention row navigates to when its action is clicked.</summary>
public enum DashboardAlertTarget
{
    Finance,
    Students,
    Attendance,
    License,
}

/// <summary>Action of a quick-action card.</summary>
public enum DashboardQuickTarget
{
    NewStudent,
    Payment,
    Attendance,
    Report,
}

/// <summary>Destination of one new-school setup step.</summary>
public enum DashboardSetupTarget
{
    Levels,
    Subjects,
    Teachers,
    Groups,
    Students,
}

/// <summary>One clickable KPI of the overview row.</summary>
public sealed record DashboardStatItem(
    string Title,
    string Value,
    string Caption,
    string Glyph,
    StatusTone Tone,
    TrendDirection Trend,
    DashboardStatTarget Target,
    IReadOnlyList<double> Sparkline);

/// <summary>One row of the today's-session timeline.</summary>
public sealed record DashboardSessionRow(
    int Id,
    string GroupName,
    string TimeText,
    string TypeText,
    string StatusText,
    string StudentsText,
    StatusTone Tone,
    bool CanTakeAttendance,
    bool HasAttendance);

/// <summary>One prioritized item of "Needs your attention".</summary>
public sealed record DashboardAlertRow(
    string Title,
    string Body,
    string Category,
    string ActionText,
    string Glyph,
    StatusTone Tone,
    DashboardAlertTarget Target);

/// <summary>One responsive quick-action card.</summary>
public sealed record DashboardQuickAction(
    string Title,
    string Hint,
    string Glyph,
    DashboardQuickTarget Target);

/// <summary>One step of the guided new-school setup.</summary>
public sealed record DashboardSetupStepItem(
    int Number,
    string Title,
    string CountText,
    string StateText,
    bool IsComplete,
    StatusTone Tone,
    DashboardSetupTarget Target);

/// <summary>
/// Phase D4 - adaptive column counts. Pure so the thresholds are unit-tested
/// without a message loop; the page feeds it the real content width from
/// <c>SizeChanged</c>. Nothing here overflows: the smallest step is one column.
/// </summary>
public static class DashboardLayout
{
    /// <summary>Content width at which the overview and the charts use three columns.</summary>
    public const double ThreeColumnWidth = 1200;

    /// <summary>Content width at which two columns replace one.</summary>
    public const double TwoColumnWidth = 760;

    public static int ColumnsFor(double width) => width switch
    {
        >= ThreeColumnWidth => 3,
        >= TwoColumnWidth => 2,
        _ => 1,
    };

    /// <summary>Quick actions are shorter, so four fit where charts use three.</summary>
    public static int QuickActionColumnsFor(double width) => width switch
    {
        >= ThreeColumnWidth => 4,
        >= TwoColumnWidth => 2,
        _ => 1,
    };

    /// <summary>Attention rows stay readable: two at most.</summary>
    public static int AttentionColumnsFor(double width) =>
        width >= TwoColumnWidth ? 2 : 1;
}
