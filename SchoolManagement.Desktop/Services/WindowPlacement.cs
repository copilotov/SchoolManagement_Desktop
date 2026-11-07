namespace SchoolManagement.Desktop.Services;

/// <summary>
/// Phase D2 - the geometry of the main window as it is persisted between runs.
/// Values are in WPF device-independent units, exactly as
/// <see cref="System.Windows.Window.Left"/> and friends report them.
/// </summary>
public sealed record WindowPlacement(
    double Left,
    double Top,
    double Width,
    double Height,
    bool IsMaximized)
{
    public const double DefaultWidth = 1280;
    public const double DefaultHeight = 820;

    /// <summary>Minimum visible width/height of the title bar kept on screen.</summary>
    private const double VisibleMargin = 80;

    public static WindowPlacement Default { get; } =
        new(double.NaN, double.NaN, DefaultWidth, DefaultHeight, false);

    /// <summary>
    /// Keeps a restored window reachable. A monitor can be unplugged, a
    /// resolution can change or a stale file can contain NaN, so every value is
    /// clamped to the current virtual desktop. The title bar always keeps at
    /// least <see cref="VisibleMargin"/> units on screen; a fully off-screen
    /// placement is re-centred instead of being restored where nobody can grab
    /// it. Pure, so it is unit-tested without a window.
    /// </summary>
    public WindowPlacement Clamp(
        double virtualLeft,
        double virtualTop,
        double virtualWidth,
        double virtualHeight,
        double minWidth,
        double minHeight)
    {
        var width = ClampLength(Width, minWidth, virtualWidth, DefaultWidth);
        var height = ClampLength(Height, minHeight, virtualHeight, DefaultHeight);

        var left = ClampPosition(Left, virtualLeft, virtualWidth, width);
        var top = ClampPosition(Top, virtualTop, virtualHeight, height);

        return this with { Left = left, Top = top, Width = width, Height = height };
    }

    private static double ClampLength(double value, double minimum, double available, double fallback)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
        {
            value = fallback;
        }

        var maximum = Math.Max(minimum, available);
        return Math.Clamp(value, minimum, maximum);
    }

    private static double ClampPosition(double value, double origin, double span, double length)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return origin + ((span - length) / 2);
        }

        var minimum = origin - length + VisibleMargin;
        var maximum = origin + span - VisibleMargin;
        return Math.Clamp(value, minimum, maximum);
    }
}
