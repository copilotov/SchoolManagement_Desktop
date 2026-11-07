using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using FluentAssertions;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Tests.Students;

/// <summary>
/// Static checks on the five Phase 5 views. A CommunityToolkit command name is
/// generated from the method name, so a hand-written binding fails silently at
/// runtime; these tests fail the build instead.
/// </summary>
public sealed class StudentViewBindingTests
{
    private static readonly Regex CommandBinding = new(
        @"\{Binding\s+(?:DataContext\.)?(?<name>[A-Za-z_][A-Za-z0-9_]*Command)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static TheoryData<string, Type> Views => new()
    {
        { "StudentsPage.xaml", typeof(StudentsViewModel) },
        { "StudentFormView.xaml", typeof(StudentsViewModel) },
        { "StudentDetailsPage.xaml", typeof(StudentDetailsViewModel) },
        { "StudentCardPage.xaml", typeof(StudentCardViewModel) },
        { "StudentsQrPrintPage.xaml", typeof(StudentsQrPrintViewModel) },
    };

    [Theory]
    [MemberData(nameof(Views))]
    public void Every_command_binding_exists_on_its_view_model(string fileName, Type viewModelType)
    {
        var names = CommandBinding.Matches(ReadView(fileName))
            .Select(match => match.Groups["name"].Value)
            .Distinct()
            .ToArray();

        names.Should().NotBeEmpty();
        foreach (var name in names)
        {
            viewModelType.GetProperty(name).Should().NotBeNull(
                $"{fileName} binds {name}, which the ViewModel must expose");
        }
    }

    [Fact]
    public void The_student_list_keeps_its_keyboard_shortcuts()
    {
        var xaml = ReadView("StudentsPage.xaml");

        xaml.Should().Contain("Key=\"F2\"");
        xaml.Should().Contain("Key=\"Delete\"");
        xaml.Should().Contain("Key=\"Escape\"");
        xaml.Should().Contain("Key=\"Enter\"");
    }

    [Fact]
    public void The_bulk_qr_page_derives_its_size_from_the_available_space()
    {
        var xaml = ReadView("StudentsQrPrintPage.xaml");

        xaml.Should().Contain("<Viewbox");
        xaml.Should().Contain("<UniformGrid");
        xaml.Should().Contain("ItemsSource=\"{Binding Tiles}\"");
    }

    private static string ReadView(string fileName)
        => File.ReadAllText(Path.Combine(ViewsDirectory(), fileName));

    private static string ViewsDirectory([CallerFilePath] string callerPath = "")
    {
        var testsDirectory = Path.GetDirectoryName(callerPath)!;
        var root = Path.GetFullPath(Path.Combine(testsDirectory, "..", ".."));
        return Path.Combine(root, "SchoolManagement.Desktop", "Views");
    }
}
