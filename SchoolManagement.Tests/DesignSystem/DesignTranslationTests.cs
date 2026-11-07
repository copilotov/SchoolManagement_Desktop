using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;
using FluentAssertions;
using SchoolManagement.Application.Localization;

namespace SchoolManagement.Tests.DesignSystem;

/// <summary>
/// Phase D1 - the debug gallery is still a shipped translation surface, so its
/// keys are guarded exactly like every other screen: one constant, three
/// languages, no orphan key left behind in a JSON file.
/// </summary>
public sealed class DesignTranslationTests
{
    private static readonly string[] Languages = ["ar", "en", "fr"];

    [Theory]
    [InlineData("ar")]
    [InlineData("en")]
    [InlineData("fr")]
    public void Every_design_constant_exists_in_every_language(string language)
    {
        var keys = LoadKeys(language);
        var constants = DesignConstants();

        constants.Should().NotBeEmpty();
        keys.Should().Contain(constants, $"every design key must exist in {language}.json");
    }

    [Fact]
    public void Design_constants_use_the_design_prefix()
    {
        DesignConstants()
            .Should().OnlyContain(key => key.StartsWith("design.", StringComparison.Ordinal));
    }

    [Fact]
    public void No_orphan_design_keys_remain_in_the_translation_files()
    {
        var constants = new HashSet<string>(DesignConstants(), StringComparer.Ordinal);

        foreach (var language in Languages)
        {
            var orphans = LoadKeys(language)
                .Where(key => key.StartsWith("design.", StringComparison.Ordinal) && !constants.Contains(key))
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();

            orphans.Should().BeEmpty(
                $"{language}.json must not carry a design.* key that no constant references");
        }
    }

    private static IReadOnlyList<string> DesignConstants()
    {
        return typeof(TranslationKeys.Design)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static HashSet<string> LoadKeys(string language)
    {
        var path = Path.Combine(DesignFiles.TranslationsDirectory, $"{language}.json");
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
}
