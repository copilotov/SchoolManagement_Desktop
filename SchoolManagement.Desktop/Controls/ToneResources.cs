namespace SchoolManagement.Desktop.Controls;

/// <summary>
/// Phase D3 - the four brushes a semantic tone needs, expressed as resource
/// keys rather than colours. A component applies them with
/// <c>SetResourceReference</c>, so the element keeps following the live theme.
/// </summary>
public readonly record struct TonePalette(
    string Background,
    string Border,
    string Foreground,
    string Solid);

/// <summary>Maps <see cref="StatusTone"/> to palette token names.</summary>
public static class ToneResources
{
    public static TonePalette For(StatusTone tone) => tone switch
    {
        StatusTone.Accent => new TonePalette(
            "Accent.Container", "Accent", "Accent.OnContainer", "Accent"),
        StatusTone.Success => new TonePalette(
            "Success.Container", "Success", "Success.OnContainer", "Success"),
        StatusTone.Warning => new TonePalette(
            "Warning.Container", "Warning", "Warning.OnContainer", "Warning"),
        StatusTone.Danger => new TonePalette(
            "Danger.Container", "Danger", "Danger.OnContainer", "Danger"),
        StatusTone.Info => new TonePalette(
            "Info.Container", "Info", "Info.OnContainer", "Info"),
        _ => new TonePalette(
            "Surface.Alt", "Border.Subtle", "Text.Secondary", "Text.Secondary"),
    };
}
