using System.IO;
using FluentAssertions;

namespace SchoolManagement.Tests.DesignSystem;

/// <summary>
/// Phase D1 - palette contract.
///
/// Three promises are enforced here:
///  1. Light and Dark define exactly the same keys, so a theme switch can never
///     leave a brush unresolved.
///  2. Every text/background pair the screens actually use reaches WCAG AA
///     (4.5:1). Disabled text is the single documented exception.
///  3. The legacy <c>Brush.*</c> aliases still mirror the semantic tokens, so
///     the Phase 1-7 screens inherit the new palette without being edited.
/// </summary>
public sealed class DesignPaletteTests
{
    /// <summary>
    /// Foreground/background pairs that must reach 4.5:1 in both themes. The
    /// list is deliberately the real usage, not every permutation: a semantic
    /// colour is never painted on another semantic colour.
    /// </summary>
    private static readonly (string Foreground, string Background)[] RequiredPairs =
    [
        ("Text.Primary", "Surface.Base"),
        ("Text.Primary", "Surface.Card"),
        ("Text.Primary", "Surface.Alt"),
        ("Text.Primary", "Surface.Sunken"),
        ("Text.Primary", "Surface.Hover"),
        ("Text.Primary", "Surface.Selected"),
        ("Text.Primary", "Surface.RowAlt"),

        ("Text.Secondary", "Surface.Base"),
        ("Text.Secondary", "Surface.Card"),
        ("Text.Secondary", "Surface.Alt"),
        ("Text.Secondary", "Surface.Sunken"),
        ("Text.Secondary", "Surface.Hover"),
        ("Text.Secondary", "Surface.Selected"),
        ("Text.Secondary", "Surface.RowAlt"),

        ("Text.OnAccent", "Accent"),
        ("Text.OnAccent", "Accent.Hover"),
        ("Text.OnAccent", "Accent.Pressed"),

        ("Accent", "Surface.Base"),
        ("Accent", "Surface.Card"),
        ("Accent", "Surface.Alt"),
        ("Accent.OnContainer", "Accent.Container"),

        ("Success", "Surface.Base"),
        ("Success", "Surface.Card"),
        ("Success.OnContainer", "Success.Container"),

        ("Warning", "Surface.Base"),
        ("Warning", "Surface.Card"),
        ("Warning.OnContainer", "Warning.Container"),

        ("Danger", "Surface.Base"),
        ("Danger", "Surface.Card"),
        ("Danger.OnContainer", "Danger.Container"),

        ("Info", "Surface.Base"),
        ("Info", "Surface.Card"),
        ("Info.OnContainer", "Info.Container"),

        ("Attendance.Present", "Surface.Card"),
        ("Attendance.Absent", "Surface.Card"),
        ("Attendance.Late", "Surface.Card"),
        ("Attendance.Excused", "Surface.Card"),
        ("Attendance.Unexcused", "Surface.Card"),

        ("Finance.Paid", "Surface.Card"),
        ("Finance.Partial", "Surface.Card"),
        ("Finance.Overdue", "Surface.Card"),
        ("Finance.Exempt", "Surface.Card"),
    ];

    /// <summary>Legacy aliases that must keep pointing at a semantic token.</summary>
    private static readonly (string Alias, string Token)[] LegacyAliases =
    [
        ("Brush.Background", "Surface.Base"),
        ("Brush.Surface", "Surface.Card"),
        ("Brush.SurfaceVariant", "Surface.Alt"),
        ("Brush.Primary", "Accent"),
        ("Brush.OnPrimary", "Text.OnAccent"),
        ("Brush.PrimaryContainer", "Accent.Container"),
        ("Brush.OnPrimaryContainer", "Accent.OnContainer"),
        ("Brush.TextPrimary", "Text.Primary"),
        ("Brush.TextSecondary", "Text.Secondary"),
        ("Brush.Border", "Border.Subtle"),
        ("Brush.NavRail", "Surface.Card"),
        ("Brush.Hover", "Surface.Hover"),
        ("Brush.Selected", "Surface.Selected"),
        ("Brush.Success", "Success"),
        ("Brush.Warning", "Warning"),
        ("Brush.Info", "Info"),
        ("Brush.Danger", "Danger"),
        ("Brush.Present", "Attendance.Present"),
        ("Brush.Late", "Attendance.Late"),
        ("Brush.Excused", "Attendance.Excused"),
        ("Brush.Unexcused", "Attendance.Unexcused"),
        ("Brush.Paid", "Finance.Paid"),
        ("Brush.PartiallyPaid", "Finance.Partial"),
        ("Brush.Unpaid", "Finance.Overdue"),
        ("Brush.Exempted", "Finance.Exempt"),
    ];

    [Fact]
    public void Light_and_dark_palettes_define_the_same_keys()
    {
        var light = DesignFiles.ReadPalette(DesignFiles.Light);
        var dark = DesignFiles.ReadPalette(DesignFiles.Dark);

        light.Should().NotBeEmpty();
        dark.Keys.Should().BeEquivalentTo(light.Keys,
            "a key that exists in only one theme leaves a DynamicResource unresolved after a theme switch");
    }

    [Theory]
    [InlineData(DesignFiles.Light)]
    [InlineData(DesignFiles.Dark)]
    public void Every_used_text_pair_meets_wcag_aa(string theme)
    {
        var palette = DesignFiles.ReadPalette(theme);
        var failures = new List<string>();

        foreach (var (foreground, background) in RequiredPairs)
        {
            palette.Should().ContainKey(foreground);
            palette.Should().ContainKey(background);

            var ratio = DesignFiles.ContrastRatio(palette[foreground], palette[background]);
            if (ratio < 4.5)
            {
                failures.Add(
                    $"{theme}: {foreground} {palette[foreground]} on {background} {palette[background]} = {ratio:F2}:1");
            }
        }

        failures.Should().BeEmpty("WCAG AA requires at least 4.5:1 for normal text");
    }

    [Theory]
    [InlineData(DesignFiles.Light)]
    [InlineData(DesignFiles.Dark)]
    public void Disabled_text_is_exempt_from_aa_but_stays_legible(string theme)
    {
        var palette = DesignFiles.ReadPalette(theme);
        var ratio = DesignFiles.ContrastRatio(palette["Text.Disabled"], palette["Surface.Base"]);

        // Disabled text is WCAG-exempt. It must still be readable as text, so
        // the floor is 2.5:1 rather than "any value at all".
        ratio.Should().BeInRange(2.5, 4.5,
            "disabled text is intentionally below AA but must not disappear into the surface");
    }

    [Theory]
    [InlineData(DesignFiles.Light)]
    [InlineData(DesignFiles.Dark)]
    public void Legacy_brush_aliases_mirror_the_semantic_tokens(string theme)
    {
        var palette = DesignFiles.ReadPalette(theme);
        var mismatches = new List<string>();

        foreach (var (alias, token) in LegacyAliases)
        {
            if (!palette.TryGetValue(alias, out var aliasColour))
            {
                mismatches.Add($"{theme}: missing legacy alias {alias}");
                continue;
            }

            if (!palette.TryGetValue(token, out var tokenColour))
            {
                mismatches.Add($"{theme}: missing semantic token {token}");
                continue;
            }

            if (aliasColour != tokenColour)
            {
                mismatches.Add($"{theme}: {alias} {aliasColour} should equal {token} {tokenColour}");
            }
        }

        mismatches.Should().BeEmpty(
            "the Phase 1-7 screens still bind Brush.* and must inherit the Phase D1 palette");
    }

    [Theory]
    [InlineData(DesignFiles.Light)]
    [InlineData(DesignFiles.Dark)]
    public void Translucent_tokens_keep_their_alpha(string theme)
    {
        var palette = DesignFiles.ReadPalette(theme);

        palette["Surface.Mica"].A.Should().BeLessThan(255,
            "the Mica surface must stay translucent so the backdrop shows through");
        palette["Overlay.Scrim"].A.Should().BeLessThan(255,
            "the modal scrim must stay translucent");
        palette["Overlay.Scrim"].A.Should().BeGreaterThan(128,
            "the modal scrim must still dim the page behind it");
    }

    [Fact]
    public void Theme_shims_still_point_at_the_design_palette()
    {
        // ThemeService swaps the dictionary whose Source contains
        // "Resources/Themes/" in place. The shim must therefore stay a shim,
        // not a second copy of the palette.
        var light = File.ReadAllText(Path.Combine(DesignFiles.ThemesDirectory, "Light.xaml"));
        var dark = File.ReadAllText(Path.Combine(DesignFiles.ThemesDirectory, "Dark.xaml"));

        light.Should().Contain("Resources/Design/Colors.Light.xaml");
        dark.Should().Contain("Resources/Design/Colors.Dark.xaml");

        DesignFiles.ContainsHexColour(light).Should().BeFalse(
            "the theme shim must not duplicate palette values");
        DesignFiles.ContainsHexColour(dark).Should().BeFalse(
            "the theme shim must not duplicate palette values");
    }
}
