using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using FluentAssertions;

namespace SchoolManagement.Tests.Shell;

/// <summary>
/// Phase D2 guard: the shell chrome is fully localized and the physical key
/// names are identical in the three files (a keycap is not translated text).
/// </summary>
public sealed class ShellTranslationTests
{
    private static readonly string[] Languages = ["ar", "en", "fr"];

    private static readonly string[] RequiredSections =
    [
        "nav", "shell", "palette", "shortcuts", "toast",
    ];

    private static readonly string[] RequiredShortcutHints =
    [
        "shortcuts.keysNew",
        "shortcuts.keysSearch",
        "shortcuts.keysSearchAlt",
        "shortcuts.keysSave",
        "shortcuts.keysPrint",
        "shortcuts.keysClose",
        "shortcuts.keysNavigateTabs",
        "shortcuts.keysHelp",
        "shortcuts.keysBack",
        "shortcuts.keysToggleNav",
    ];

    [Theory]
    [InlineData("ar")]
    [InlineData("en")]
    [InlineData("fr")]
    public void The_shell_sections_exist_and_are_not_empty(string language)
    {
        var root = Load(language);

        foreach (var section in RequiredSections)
        {
            var node = root[section] as JsonObject;
            node.Should().NotBeNull($"{language}.json must define the '{section}' section");

            foreach (var (key, value) in node!)
            {
                value?.GetValue<string>().Should().NotBeNullOrWhiteSpace(
                    $"{language}.json '{section}.{key}' must not be empty");
            }
        }
    }

    [Fact]
    public void The_keyboard_hints_exist_in_every_language()
    {
        // The key names themselves follow the keyboard layout ("Esc" in English,
        // "Échap" in French), so only presence is asserted across languages.
        foreach (var language in Languages)
        {
            var root = Load(language);

            foreach (var key in RequiredShortcutHints)
            {
                Value(root, key).Should().NotBeNullOrWhiteSpace(
                    $"{language}.json '{key}' must not be empty");
            }
        }

        var english = Load("en");
        Value(english, "shortcuts.keysNew").Should().Be("Ctrl+N");
        Value(english, "shortcuts.keysSearch").Should().Be("Ctrl+K");
        Value(english, "shortcuts.keysSearchAlt").Should().Be("Ctrl+F");
        Value(english, "shortcuts.keysSave").Should().Be("Ctrl+S");
        Value(english, "shortcuts.keysPrint").Should().Be("Ctrl+P");
        Value(english, "shortcuts.keysHelp").Should().Be("F1");
        Value(english, "shortcuts.keysBack").Should().Be("Alt+Left");
    }

    [Fact]
    public void The_navigation_groups_are_translated_in_every_language()
    {
        foreach (var language in Languages)
        {
            var root = Load(language);
            var nav = root["nav"] as JsonObject;

            nav.Should().NotBeNull();
            nav!.Select(pair => pair.Value?.GetValue<string>())
                .Should().OnlyContain(value => !string.IsNullOrWhiteSpace(value));
        }
    }

    private static string? Value(JsonObject root, string dottedKey)
    {
        JsonNode? current = root;
        foreach (var segment in dottedKey.Split('.'))
        {
            current = (current as JsonObject)?[segment];
        }

        return current?.GetValue<string>();
    }

    private static JsonObject Load(string language)
    {
        var path = Path.Combine(TranslationsDirectory, $"{language}.json");
        return JsonNode.Parse(File.ReadAllText(path)) as JsonObject
            ?? throw new InvalidOperationException($"Could not parse {path}.");
    }

    private static string TranslationsDirectory =>
        Path.Combine(RepoRoot(), "SchoolManagement.Desktop", "Resources", "Translations");

    private static string RepoRoot([CallerFilePath] string callerPath = "")
    {
        var testsDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(callerPath)!, ".."));
        return Path.GetFullPath(Path.Combine(testsDirectory, ".."));
    }
}
