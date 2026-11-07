using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using FluentAssertions;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Tests.Groups;

/// <summary>
/// Static checks on the seven Phase 6 views. A CommunityToolkit command name is
/// generated from the method name, so a hand-written binding fails silently at
/// runtime; these tests fail the build instead.
/// </summary>
public sealed class GroupViewBindingTests
{
    private static readonly Regex CommandBinding = new(
        @"\{Binding\s+(?:DataContext\.)?(?<name>[A-Za-z_][A-Za-z0-9_]*Command)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static TheoryData<string, Type> Views => new()
    {
        { "GroupsPage.xaml", typeof(GroupsViewModel) },
        { "GroupFormView.xaml", typeof(GroupsViewModel) },
        { "GroupDetailsPage.xaml", typeof(GroupDetailsViewModel) },
        { "GroupSchedulePage.xaml", typeof(GroupScheduleViewModel) },
        { "EnrollmentPage.xaml", typeof(EnrollmentViewModel) },
        { "TimetablePage.xaml", typeof(TimetableViewModel) },
        { "GroupFullReportPage.xaml", typeof(GroupFullReportViewModel) },
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
    public void The_group_list_keeps_its_keyboard_shortcuts()
    {
        var xaml = ReadView("GroupsPage.xaml");

        xaml.Should().Contain("Key=\"F2\"");
        xaml.Should().Contain("Key=\"Delete\"");
        xaml.Should().Contain("Key=\"Escape\"");
        xaml.Should().Contain("Key=\"Enter\"");
    }

    [Fact]
    public void The_schedule_editor_keeps_its_keyboard_shortcuts()
    {
        var xaml = ReadView("GroupSchedulePage.xaml");

        xaml.Should().Contain("Key=\"Escape\"");
        xaml.Should().Contain("Key=\"Enter\"");
    }

    [Fact]
    public void The_timetable_derives_its_grid_from_the_view_model_counts()
    {
        var xaml = ReadView("TimetablePage.xaml");

        xaml.Should().Contain("<UniformGrid");
        xaml.Should().Contain("Columns=\"{Binding ColumnCount}\"");
        xaml.Should().Contain("Rows=\"{Binding RowCount}\"");
        xaml.Should().Contain("ItemsSource=\"{Binding Cells}\"");
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
