using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SchoolManagement.Tests.DesignSystem;

/// <summary>
/// Locates the design-system source files and reads the theme palettes.
///
/// The tests read the real XAML and the real JSON, not a copy, so a colour, a
/// key or a dictionary path that drifts fails the suite instead of only
/// showing up as a wrong pixel on screen.
/// </summary>
internal static class DesignFiles
{
    public const string Light = "Light";
    public const string Dark = "Dark";

    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly Regex DynamicResourceRegex = new(
        @"\{DynamicResource\s+([A-Za-z0-9_.]+)\s*\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex StaticResourceRegex = new(
        @"\{StaticResource\s+([^}]+)\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex HexColourRegex = new(
        @"#[0-9A-Fa-f]{3,8}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string RepoRoot([CallerFilePath] string callerPath = "")
    {
        var testsDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(callerPath)!, ".."));
        return Path.GetFullPath(Path.Combine(testsDirectory, ".."));
    }

    public static string DesktopDirectory => Path.Combine(RepoRoot(), "SchoolManagement.Desktop");

    public static string DesignDirectory => Path.Combine(DesktopDirectory, "Resources", "Design");

    public static string StylesDirectory => Path.Combine(DesktopDirectory, "Resources", "Styles");

    public static string ThemesDirectory => Path.Combine(DesktopDirectory, "Resources", "Themes");

    public static string ViewsDirectory => Path.Combine(DesktopDirectory, "Views");

    /// <summary>Phase D3 - the reusable component library.</summary>
    public static string ControlsDirectory => Path.Combine(DesktopDirectory, "Controls");

    public static string TranslationsDirectory => Path.Combine(DesktopDirectory, "Resources", "Translations");

    public static string PalettePath(string theme)
        => Path.Combine(DesignDirectory, $"Colors.{theme}.xaml");

    /// <summary>Every <c>SolidColorBrush</c> keyed by <c>x:Key</c>, in file order.</summary>
    public static Dictionary<string, Rgba> ReadPalette(string theme)
    {
        var document = XDocument.Load(PalettePath(theme));
        var palette = new Dictionary<string, Rgba>(StringComparer.Ordinal);

        foreach (var element in document.Descendants())
        {
            if (element.Name.LocalName != "SolidColorBrush")
            {
                continue;
            }

            var key = (string?)element.Attribute(Xaml + "Key");
            var colour = (string?)element.Attribute("Color");
            if (key is null || colour is null)
            {
                continue;
            }

            palette[key] = ParseColour(colour);
        }

        return palette;
    }

    public static Rgba ParseColour(string value)
    {
        var hex = value.TrimStart('#');
        if (hex.Length == 6)
        {
            hex = "FF" + hex;
        }

        if (hex.Length != 8)
        {
            throw new FormatException($"'{value}' is not a #RRGGBB or #AARRGGBB colour.");
        }

        return new Rgba(
            byte.Parse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(hex[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(hex[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(hex[6..8], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// WCAG 2.1 contrast ratio. A translucent foreground is composited over the
    /// background first, so a scrim or a Mica surface is measured the way the
    /// user actually sees it.
    /// </summary>
    public static double ContrastRatio(Rgba foreground, Rgba background)
    {
        var alpha = foreground.A / 255d;
        var red = Composite(foreground.R, background.R, alpha);
        var green = Composite(foreground.G, background.G, alpha);
        var blue = Composite(foreground.B, background.B, alpha);

        var foregroundLuminance = Luminance(red, green, blue);
        var backgroundLuminance = Luminance(background.R, background.G, background.B);

        var lighter = Math.Max(foregroundLuminance, backgroundLuminance);
        var darker = Math.Min(foregroundLuminance, backgroundLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    public static IEnumerable<string> EnumerateXamlFiles(string directory)
        => Directory
            .EnumerateFiles(directory, "*.xaml", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .OrderBy(path => path, StringComparer.Ordinal);

    public static IEnumerable<string> EnumerateResourceDictionaries()
        => EnumerateXamlFiles(Path.Combine(DesktopDirectory, "Resources"));

    public static IReadOnlyCollection<string> DynamicResourceKeysIn(string xaml)
        => DynamicResourceRegex
            .Matches(xaml)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    /// <summary>
    /// Every <c>{StaticResource key}</c> reference, including the keys a view
    /// declares locally in its own <c>Resources</c> section.
    /// </summary>
    public static IReadOnlyCollection<string> StaticResourceKeysIn(string xaml)
        => StaticResourceRegex
            .Matches(xaml)
            .Select(match => match.Groups[1].Value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    public static bool ContainsHexColour(string xaml) => HexColourRegex.IsMatch(xaml);

    public static string Relative(string path)
        => Path.GetRelativePath(RepoRoot(), path);

    /// <summary>
    /// True when <paramref name="token"/> appears inside a <c>#if DEBUG</c> block.
    /// Used to prove the design gallery cannot leak into a release build.
    /// </summary>
    public static bool IsInsideDebugBlock(string source, string token)
    {
        var depth = 0;
        foreach (var line in source.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("#if DEBUG", StringComparison.Ordinal))
            {
                depth++;
                continue;
            }

            if (trimmed.StartsWith("#endif", StringComparison.Ordinal))
            {
                depth--;
                continue;
            }

            if (trimmed.Contains(token, StringComparison.Ordinal) && depth > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static byte Composite(byte foreground, byte background, double alpha)
        => (byte)Math.Round((foreground * alpha) + (background * (1 - alpha)));

    private static double Luminance(byte red, byte green, byte blue)
        => (0.2126 * Channel(red)) + (0.7152 * Channel(green)) + (0.0722 * Channel(blue));

    private static double Channel(byte value)
    {
        var channel = value / 255d;
        return channel <= 0.03928
            ? channel / 12.92
            : Math.Pow((channel + 0.055) / 1.055, 2.4);
    }

    private static bool IsBuildOutput(string path)
        => path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
}

/// <summary>An 8-bit sRGB colour with alpha, as written in the palette XAML.</summary>
internal readonly record struct Rgba(byte A, byte R, byte G, byte B)
{
    public override string ToString() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";
}
