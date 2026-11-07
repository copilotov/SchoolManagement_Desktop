using System.IO;
using System.Runtime.CompilerServices;
using FluentAssertions;
using SchoolManagement.Infrastructure.Licensing.Ed25519;

namespace SchoolManagement.Tests.Licensing;

/// <summary>
/// Structural rules that are easy to break later: the license decision path
/// must never read the raw system clock directly, the issuer tool must never be
/// shipped, and no private key material may be tracked.
/// </summary>
public sealed class LicenseArchitectureTests
{
    private static string RepositoryRoot([CallerFilePath] string thisFile = "")
    {
        var testsDirectory = Path.GetDirectoryName(thisFile)!;
        return Path.GetFullPath(Path.Combine(testsDirectory, "..", ".."));
    }

    private static IEnumerable<string> SourceFiles(params string[] relativeDirectories)
    {
        var root = RepositoryRoot();
        foreach (var relative in relativeDirectories)
        {
            var directory = Path.Combine(root, relative);
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                yield return file;
            }
        }
    }

    private static string StripLineComments(string line)
    {
        var index = line.IndexOf("//", StringComparison.Ordinal);
        return index >= 0 ? line[..index] : line;
    }

    [Fact]
    public void License_decision_code_never_reads_the_system_clock_directly()
    {
        var offenders = new List<string>();

        foreach (var file in SourceFiles(
                     Path.Combine("SchoolManagement.Domain", "Licensing"),
                     Path.Combine("SchoolManagement.Application", "Licensing"),
                     Path.Combine("SchoolManagement.Infrastructure", "Licensing")))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var code = StripLineComments(lines[i]);
                if (code.Contains("DateTime.Now", StringComparison.Ordinal) ||
                    code.Contains("DateTime.UtcNow", StringComparison.Ordinal))
                {
                    offenders.Add($"{Path.GetFileName(file)}:{i + 1}");
                }
            }
        }

        offenders.Should().BeEmpty("license validity must read ITrustedClock, never the raw clock");
    }

    [Fact]
    public void The_single_raw_clock_wrapper_is_the_system_local_clock()
    {
        var wrapper = Path.Combine(
            RepositoryRoot(),
            "SchoolManagement.Infrastructure",
            "Time",
            "SystemLocalClock.cs");

        File.Exists(wrapper).Should().BeTrue();
        var code = File.ReadAllText(wrapper);
        code.Should().Contain("DateTime.UtcNow");
        code.Should().Contain("DateTime.Now");
    }

    [Fact]
    public void The_public_key_is_a_compile_time_constant()
    {
        var field = typeof(LicensePublicKey).GetField(nameof(LicensePublicKey.Base64));

        field.Should().NotBeNull();
        field!.IsLiteral.Should().BeTrue();
        LicensePublicKey.Base64.Should().Be(
            Convert.ToBase64String(LicensePublicKey.ToArray()));
        LicensePublicKey.ToArray().Should().HaveCount(32);
    }

    [Fact]
    public void The_issuer_tool_is_not_referenced_by_any_shipped_project()
    {
        var root = RepositoryRoot();
        var shippedProjects = new[]
        {
            Path.Combine(root, "SchoolManagement.Domain", "SchoolManagement.Domain.csproj"),
            Path.Combine(root, "SchoolManagement.Application", "SchoolManagement.Application.csproj"),
            Path.Combine(root, "SchoolManagement.Infrastructure", "SchoolManagement.Infrastructure.csproj"),
            Path.Combine(root, "SchoolManagement.Desktop", "SchoolManagement.Desktop.csproj"),
        };

        foreach (var project in shippedProjects)
        {
            File.Exists(project).Should().BeTrue();
            File.ReadAllText(project).Should().NotContain(
                "LicenseTool",
                "the signing tool must never be linked into a shipped assembly");
        }
    }

    [Fact]
    public void No_private_key_files_are_tracked_in_source_directories()
    {
        var root = RepositoryRoot();
        var forbidden = new[] { ".key", ".pem", ".pfx", ".p12" };

        var offenders = Directory
            .EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}.vs{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => forbidden.Any(extension =>
                path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void Gitignore_covers_private_key_material()
    {
        var gitignore = Path.Combine(RepositoryRoot(), ".gitignore");

        File.Exists(gitignore).Should().BeTrue();
        var content = File.ReadAllText(gitignore);
        content.Should().Contain("*.key");
        content.Should().Contain("*.pem");
        content.Should().Contain(".secrets/");
    }
}
