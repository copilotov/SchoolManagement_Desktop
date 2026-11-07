using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Tests.Shell;

public sealed class WindowStateStoreTests
{
    [Fact]
    public async Task A_missing_file_falls_back_to_the_default_placement()
    {
        var path = NewPath();
        var store = CreateStore(path);

        var placement = await store.LoadAsync();

        placement.Should().Be(WindowPlacement.Default);
    }

    [Fact]
    public async Task A_saved_placement_round_trips()
    {
        var path = NewPath();
        var store = CreateStore(path);
        var placement = new WindowPlacement(-120, 40, 1440, 900, true);

        (await store.SaveAsync(placement)).Should().BeTrue();

        File.Exists(path).Should().BeTrue();
        (await store.LoadAsync()).Should().Be(placement);
    }

    [Fact]
    public async Task A_corrupt_file_falls_back_instead_of_throwing()
    {
        var path = NewPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "{ this is not json");
        var store = CreateStore(path);

        var placement = await store.LoadAsync();

        placement.Should().Be(WindowPlacement.Default);
    }

    [Fact]
    public async Task Saving_twice_overwrites_atomically_without_leaving_a_temporary_file()
    {
        var path = NewPath();
        var store = CreateStore(path);

        (await store.SaveAsync(new WindowPlacement(0, 0, 1280, 820, false))).Should().BeTrue();
        (await store.SaveAsync(new WindowPlacement(10, 10, 1000, 650, false))).Should().BeTrue();

        (await store.LoadAsync()).Width.Should().Be(1000);
        File.Exists($"{path}.tmp").Should().BeFalse();
    }

    [Fact]
    public void The_default_path_is_under_the_application_data_folder()
    {
        var path = WindowStateStore.DefaultPath();

        Path.GetFileName(path).Should().Be("window.json");
        Path.GetFileName(Path.GetDirectoryName(path)).Should().Be("SchoolManagement");
    }

    private static string NewPath()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "schoolmanagement-tests",
            Guid.NewGuid().ToString("N"));

        return Path.Combine(directory, "window.json");
    }

    private static WindowStateStore CreateStore(string path) =>
        new(path, NullLogger<WindowStateStore>.Instance);
}
