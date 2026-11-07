using System.Windows;

namespace SchoolManagement.Desktop.Services;

/// <summary>
/// Single source of truth for "may this app animate?".
///
/// Windows exposes the user's "Animation effects" preference through
/// <see cref="SystemParameters.ClientAreaAnimation"/>. When it is off, every
/// storyboard, spinner and hover lift in the design system must be skipped so
/// the app respects the OS accessibility setting instead of ignoring it.
/// </summary>
public static class MotionSettings
{
    /// <summary>True when the OS allows animation effects.</summary>
    public static bool IsMotionEnabled => SystemParameters.ClientAreaAnimation;
}
