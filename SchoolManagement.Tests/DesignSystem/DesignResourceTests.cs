using System.IO;
using System.Xml.Linq;
using FluentAssertions;

namespace SchoolManagement.Tests.DesignSystem;

/// <summary>
/// Phase D1 - resource wiring and view hygiene.
///
/// The theme contract only holds if the screens use <c>DynamicResource</c> and
/// never a literal colour, and if <c>App.xaml</c> merges the dictionaries in
/// the order <c>ThemeService</c> expects. Both are checked statically here, so
/// a hardcoded hex or a renamed token fails the build instead of shipping.
/// </summary>
public sealed class DesignResourceTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>Phase D3 - views and the reusable component library.</summary>
    private static IEnumerable<string> ScreenAndComponentXaml()
        => DesignFiles.EnumerateXamlFiles(DesignFiles.ViewsDirectory)
            .Concat(DesignFiles.EnumerateXamlFiles(DesignFiles.ControlsDirectory));

    [Fact]
    public void Views_and_controls_do_not_hardcode_hex_colours()
    {
        var offenders = new List<string>();

        foreach (var path in ScreenAndComponentXaml())
        {
            if (DesignFiles.ContainsHexColour(File.ReadAllText(path)))
            {
                offenders.Add(DesignFiles.Relative(path));
            }
        }

        offenders.Should().BeEmpty(
            "every colour must come from a palette token so light/dark switching repaints instantly");
    }

    [Fact]
    public void Every_dynamic_resource_key_used_in_views_and_controls_is_defined()
    {
        var defined = DefinedKeys();

        defined.Should().NotBeEmpty();

        var missing = new List<string>();
        foreach (var path in ScreenAndComponentXaml())
        {
            foreach (var key in DesignFiles.DynamicResourceKeysIn(File.ReadAllText(path)))
            {
                if (!defined.Contains(key))
                {
                    missing.Add($"{DesignFiles.Relative(path)} -> {key}");
                }
            }
        }

        missing.Should().BeEmpty(
            "a DynamicResource key that no dictionary defines resolves to nothing at runtime");
    }

    [Fact]
    public void Every_static_resource_key_used_in_views_and_controls_is_defined()
    {
        // StaticResource is resolved while the view is loaded, so a missing key
        // is a hard startup crash (it took down ShellWindow once). Local view
        // resources count too, which is why the keys are collected per file.
        var global = DefinedKeys();

        global.Should().NotBeEmpty();

        var missing = new List<string>();
        foreach (var path in ScreenAndComponentXaml())
        {
            var xaml = File.ReadAllText(path);
            var local = XDocument.Parse(xaml)
                .Descendants()
                .Select(element => element.Attribute(Xaml + "Key")?.Value)
                .Where(key => key is not null)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var key in DesignFiles.StaticResourceKeysIn(xaml))
            {
                // Nested markup extensions such as {StaticResource {x:Type DataGrid}}
                // are not plain keys and are resolved by WPF itself.
                if (key.StartsWith('{'))
                {
                    continue;
                }

                if (!global.Contains(key) && !local.Contains(key))
                {
                    missing.Add($"{DesignFiles.Relative(path)} -> {key}");
                }
            }
        }

        missing.Should().BeEmpty(
            "a StaticResource key that no dictionary defines throws while the view is loaded");
    }

    private static HashSet<string> DefinedKeys()
    {
        var defined = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in DesignFiles.EnumerateResourceDictionaries())
        {
            foreach (var element in XDocument.Load(path).Descendants())
            {
                if (element.Attribute(Xaml + "Key")?.Value is { } key)
                {
                    defined.Add(key);
                }
            }
        }

        return defined;
    }

    [Fact]
    public void App_merges_the_library_then_the_theme_then_the_tokens_then_the_styles()
    {
        // Resource lookup walks merged dictionaries from last to first, so the
        // order is the override order: component styles must come last, the
        // palette before them, and the library defaults first. ThemeService
        // replaces the dictionary whose Source contains "Resources/Themes/",
        // so that shim must also exist.
        var app = File.ReadAllText(Path.Combine(DesignFiles.DesktopDirectory, "App.xaml"));

        var order = new[]
        {
            "<ui:ThemesDictionary",
            "<ui:ControlsDictionary",
            "Resources/Themes/Light.xaml",
            "Resources/Design/Base.xaml",
            "Resources/Design/Typography.xaml",
            "Resources/Design/Spacing.xaml",
            "Resources/Design/Radii.xaml",
            "Resources/Design/Elevation.xaml",
            "Resources/Design/Icons.xaml",
            "Resources/Design/Animations.xaml",
            "Resources/Styles/Buttons.xaml",
            "Resources/Styles/Inputs.xaml",
            "Resources/Styles/DataViews.xaml",
            "Resources/Styles/Navigation.xaml",
            "Resources/Styles/Feedback.xaml",
            "Resources/Styles/Components.xaml",
        };

        var positions = order
            .Select(token => (Token: token, Index: app.IndexOf(token, StringComparison.Ordinal)))
            .ToArray();

        positions.Where(item => item.Index < 0)
            .Select(item => item.Token)
            .Should().BeEmpty("every foundation dictionary must be merged by App.xaml");

        positions.Select(item => item.Index)
            .Should().BeInAscendingOrder("the merge order defines the override order");
    }

    [Fact]
    public void Every_resource_dictionary_is_well_formed_xaml()
    {
        var failures = new List<string>();

        foreach (var path in DesignFiles.EnumerateResourceDictionaries())
        {
            try
            {
                XDocument.Load(path);
            }
            catch (Exception exception)
            {
                failures.Add($"{DesignFiles.Relative(path)}: {exception.Message}");
            }
        }

        failures.Should().BeEmpty();
    }

    [Fact]
    public void Design_gallery_is_registered_and_hooked_only_in_debug_builds()
    {
        var services = File.ReadAllText(
            Path.Combine(DesignFiles.DesktopDirectory, "DependencyInjection", "DesktopServiceCollectionExtensions.cs"));
        var shell = File.ReadAllText(Path.Combine(DesignFiles.ViewsDirectory, "ShellWindow.xaml.cs"));

        DesignFiles.IsInsideDebugBlock(services, "DesignGalleryWindow")
            .Should().BeTrue("the gallery window must not be resolvable in a release build");

        DesignFiles.IsInsideDebugBlock(services, "DesignGalleryViewModel")
            .Should().BeTrue("the gallery view model must not be resolvable in a release build");

        // Phase D2: OnPreviewKeyDown now hosts the global shortcuts, so it must
        // exist in release. Only the F12 gallery hook stays debug-only.
        DesignFiles.IsInsideDebugBlock(shell, "Key.F12")
            .Should().BeTrue("the F12 developer shortcut must be compiled out of release builds");
    }

    [Fact]
    public void Design_tokens_are_named_by_role_not_by_value()
    {
        // A token called "Teal500" or "Grey200" bakes a hue into every screen.
        // Roles survive a palette change; numbers do not.
        var palette = DesignFiles.ReadPalette(DesignFiles.Light);
        var valueNamed = palette.Keys
            .Where(key => key.Any(char.IsDigit) && !key.StartsWith("Brush.", StringComparison.Ordinal))
            .ToArray();

        valueNamed.Should().BeEmpty("design tokens must name a role, not a colour step");
    }
}
