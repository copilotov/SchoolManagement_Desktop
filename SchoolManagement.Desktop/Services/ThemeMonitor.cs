namespace SchoolManagement.Desktop.Services;

/// <summary>
/// Phase D3 - a process-wide theme pulse for controls that cannot repaint from
/// a <c>DynamicResource</c> alone.
///
/// A custom-drawn control (the sparkline) takes its brush from a
/// DynamicResource and needs nothing here. A library that consumes concrete
/// <c>SKColor</c> values (LiveCharts2) does, so <see cref="ThemeService"/>
/// raises this event after the palette dictionary has been swapped.
/// </summary>
public static class ThemeMonitor
{
    public static event EventHandler? ThemeChanged;

    public static void NotifyThemeChanged() => ThemeChanged?.Invoke(null, EventArgs.Empty);
}
