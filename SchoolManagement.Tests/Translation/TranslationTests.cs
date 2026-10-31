using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FluentAssertions;
using SchoolManagement.Application.Localization;

namespace SchoolManagement.Tests.Translation;

public sealed class TranslationTests
{
    private static readonly string[] Languages = ["ar", "en", "fr"];

    private static readonly Regex UsedKeyRegex = new(
        @"\{loc:Tr\s+([A-Za-z0-9_.]+)\s*\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex VisibleTextAttributeRegex = new(
        @"\b(?<name>Text|Content|Header|ToolTip|Title|Watermark|PlaceholderText)\s*=\s*""(?<value>[^""]*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SymbolOnlyRegex = new(
        @"^(?:&#x[0-9A-Fa-f]+;|&#\d+;|[\s\d\p{P}\p{S}]*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex LetterRegex = new(
        @"[A-Za-z\u0600-\u06FF]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ArabicRegex = new(
        @"[\u0600-\u06FF]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void All_language_files_have_the_same_keys()
    {
        var arabic = LoadKeys("ar");
        var english = LoadKeys("en");
        var french = LoadKeys("fr");

        english.Should().BeEquivalentTo(arabic);
        french.Should().BeEquivalentTo(arabic);
    }

    [Theory]
    [InlineData("ar")]
    [InlineData("en")]
    [InlineData("fr")]
    public void Every_translation_constant_exists_in_every_language(string language)
    {
        var keys = LoadKeys(language);
        var constants = GetTranslationConstants();

        constants.Should().NotBeEmpty();
        keys.Should().Contain(constants, $"every TranslationKeys constant must exist in {language}.json");
    }

    [Fact]
    public void Every_key_used_by_loc_Tr_exists_in_every_language()
    {
        var usedKeys = FindUsedKeysInXaml();
        usedKeys.Should().NotBeEmpty();

        foreach (var language in Languages)
        {
            var keys = LoadKeys(language);
            keys.Should().Contain(usedKeys, $"XAML keys must exist in {language}.json");
        }
    }

    [Fact]
    public void Xaml_does_not_contain_hardcoded_user_visible_text()
    {
        var violations = new List<string>();

        foreach (var file in EnumerateSourceFiles("*.xaml"))
        {
            var content = File.ReadAllText(file);
            foreach (Match match in VisibleTextAttributeRegex.Matches(content))
            {
                var value = match.Groups["value"].Value;
                if (value.Length == 0 || value.StartsWith("{", StringComparison.Ordinal))
                {
                    continue;
                }

                if (SymbolOnlyRegex.IsMatch(value) || !LetterRegex.IsMatch(value))
                {
                    continue;
                }

                violations.Add($"{Relative(file)}: {match.Groups["name"].Value}=\"{value}\"");
            }
        }

        violations.Should().BeEmpty();
    }

    [Fact]
    public void Desktop_code_does_not_contain_hardcoded_arabic_text()
    {
        var violations = new List<string>();

        foreach (var pattern in new[] { "*.cs", "*.xaml" })
        {
            foreach (var file in EnumerateSourceFiles(pattern))
            {
                if (ArabicRegex.IsMatch(File.ReadAllText(file)))
                {
                    violations.Add(Relative(file));
                }
            }
        }

        violations.Should().BeEmpty();
    }

    private static IReadOnlyList<string> GetTranslationConstants()
    {
        return typeof(TranslationKeys)
            .GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .SelectMany(type => type.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy))
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> FindUsedKeysInXaml()
    {
        return EnumerateSourceFiles("*.xaml")
            .SelectMany(file => UsedKeyRegex.Matches(File.ReadAllText(file)).Select(match => match.Groups[1].Value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static HashSet<string> LoadKeys(string language)
    {
        var path = Path.Combine(TranslationsDirectory, $"{language}.json");
        var root = JsonNode.Parse(File.ReadAllText(path))
            ?? throw new InvalidOperationException($"Could not parse {path}.");

        var keys = new HashSet<string>(StringComparer.Ordinal);
        Flatten(root, string.Empty, keys);
        return keys;
    }

    private static void Flatten(JsonNode? node, string prefix, ISet<string> keys)
    {
        if (node is not JsonObject jsonObject)
        {
            if (!string.IsNullOrEmpty(prefix))
            {
                keys.Add(prefix);
            }

            return;
        }

        foreach (var (key, value) in jsonObject)
        {
            var fullKey = prefix.Length == 0 ? key : $"{prefix}.{key}";
            Flatten(value, fullKey, keys);
        }
    }

    private static IEnumerable<string> EnumerateSourceFiles(string pattern)
    {
        return Directory
            .EnumerateFiles(DesktopDirectory, pattern, SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
    }

    private static string Relative(string path)
        => Path.GetRelativePath(RepoRoot(), path);

    private static string DesktopDirectory => Path.Combine(RepoRoot(), "SchoolManagement.Desktop");

    private static string TranslationsDirectory
        => Path.Combine(DesktopDirectory, "Resources", "Translations");

    private static string RepoRoot([CallerFilePath] string callerPath = "")
    {
        var testsDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(callerPath)!, ".."));
        return Path.GetFullPath(Path.Combine(testsDirectory, ".."));
    }
}
