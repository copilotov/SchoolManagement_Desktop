using System.IO;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolManagement.Infrastructure.Localization;

namespace SchoolManagement.Tests.Localization;

public sealed class LocalizationServiceTests
{
    [Fact]
    public async Task Initialize_loads_arabic_by_default_and_sets_rtl()
    {
        var localization = CreateService();

        var loaded = await localization.InitializeAsync();

        loaded.Should().BeTrue();
        localization.CurrentLanguage.Should().Be("ar");
        localization.IsRightToLeft.Should().BeTrue();
        localization.HasKey("app.name").Should().BeTrue();
        localization.Tr("app.name").Should().NotBeNullOrWhiteSpace().And.NotBe("app.name");
    }

    [Fact]
    public async Task SetLanguage_switches_instantly_and_notifies_the_indexer()
    {
        var localization = CreateService();
        await localization.InitializeAsync("ar");

        var changedProperties = new List<string?>();
        localization.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        var switched = await localization.SetLanguageAsync("en");

        switched.Should().BeTrue();
        localization.CurrentLanguage.Should().Be("en");
        localization.IsRightToLeft.Should().BeFalse();
        localization.Tr("app.name").Should().Be("School Management System");
        changedProperties.Should().Contain("Item[]");
        changedProperties.Should().Contain(nameof(localization.CurrentLanguage));
    }

    [Fact]
    public async Task Missing_key_returns_its_last_segment_and_is_recorded()
    {
        var localization = CreateService();
        await localization.InitializeAsync("en");

        var text = localization.Tr("students.thisKeyDoesNotExist");

        text.Should().Be("thisKeyDoesNotExist");
        localization.MissingKeys.Should().Contain("students.thisKeyDoesNotExist");
    }

    [Fact]
    public async Task Parameters_are_replaced_in_the_translation()
    {
        var localization = CreateService();
        await localization.InitializeAsync("en");

        var text = localization.Tr(
            "errors.fetchStudents",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["error"] = "disk full" });

        text.Should().Be("Failed to load students: disk full");
    }

    [Fact]
    public async Task Unknown_language_falls_back_to_arabic_on_initialize()
    {
        var localization = CreateService();

        var loaded = await localization.InitializeAsync("de");

        loaded.Should().BeTrue();
        localization.CurrentLanguage.Should().Be("ar");
    }

    [Fact]
    public async Task SetLanguage_keeps_the_current_language_when_the_file_is_missing()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"school-loc-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(directory, "ar.json"),
                """{"app":{"name":"ArabicName"}}""");

            var localization = CreateService(directory);
            (await localization.InitializeAsync("ar")).Should().BeTrue();

            var switched = await localization.SetLanguageAsync("en");

            switched.Should().BeFalse();
            localization.CurrentLanguage.Should().Be("ar");
            localization.Tr("app.name").Should().Be("ArabicName");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Tr_before_initialize_returns_the_last_segment()
    {
        var localization = CreateService();

        localization.Tr("menu.students").Should().Be("students");
        localization.HasKey("menu.students").Should().BeFalse();
    }

    private static LocalizationService CreateService(string? directory = null)
        => new(directory ?? FindTranslationsDirectory(), NullLogger<LocalizationService>.Instance);

    private static string FindTranslationsDirectory([CallerFilePath] string callerPath = "")
    {
        var output = Path.Combine(AppContext.BaseDirectory, "Resources", "Translations");
        if (File.Exists(Path.Combine(output, "ar.json")))
        {
            return output;
        }

        var testsDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(callerPath)!, ".."));
        var root = Path.GetFullPath(Path.Combine(testsDirectory, ".."));
        return Path.Combine(root, "SchoolManagement.Desktop", "Resources", "Translations");
    }
}
