using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using FluentAssertions;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Tests.Attendance;

/// <summary>
/// Static checks on the Phase 7 views. A CommunityToolkit command name is
/// generated from the method name, so a hand-written binding fails silently at
/// runtime; these tests fail the build instead. The keyboard shortcuts are also
/// asserted here because a lost shortcut is invisible to the compiler.
/// </summary>
public sealed class AttendanceViewBindingTests
{
    private static readonly Regex CommandBinding = new(
        @"\{Binding\s+(?:DataContext\.)?(?<name>[A-Za-z_][A-Za-z0-9_]*Command)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static TheoryData<string, Type> Views => new()
    {
        { "AttendancePage.xaml", typeof(AttendanceViewModel) },
        { "TakeAttendancePage.xaml", typeof(TakeAttendanceViewModel) },
        { "HomeworkReportPage.xaml", typeof(HomeworkViewModel) },
        { "AttendanceReportPage.xaml", typeof(AttendanceReportViewModel) },
        { "QrScannerWindow.xaml", typeof(QrScannerViewModel) },
    };

    /// <summary>
    /// Commands a row DataTemplate binds on its own item, not on the page
    /// ViewModel. They are listed explicitly so a typo still fails the test.
    /// </summary>
    private static readonly Dictionary<string, (Type RowType, string[] Commands)> RowCommands = new()
    {
        ["TakeAttendancePage.xaml"] = (
            typeof(AttendanceStudentRow),
            ["MarkPresentCommand", "MarkAbsentCommand", "MarkLateCommand"]),
        ["HomeworkReportPage.xaml"] = (
            typeof(HomeworkSubmissionRow),
            ["MarkSubmittedCommand", "MarkLateCommand", "MarkMissingCommand", "MarkPendingCommand"]),
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

        RowCommands.TryGetValue(fileName, out var rows);

        foreach (var name in names)
        {
            var owner = rows.Commands is not null && rows.Commands.Contains(name)
                ? rows.RowType
                : viewModelType;

            owner.GetProperty(name).Should().NotBeNull(
                $"{fileName} binds {name}, which {owner.Name} must expose");
        }
    }

    [Fact]
    public void The_attendance_list_keeps_its_shortcuts()
    {
        var xaml = ReadView("AttendancePage.xaml");

        xaml.Should().Contain("<KeyBinding Key=\"F5\" Command=\"{Binding ReloadCommand}\" />");
        xaml.Should().Contain(
            "<KeyBinding Key=\"G\" Modifiers=\"Control\" Command=\"{Binding GenerateSessionsCommand}\" />");
    }

    [Fact]
    public void The_take_attendance_page_keeps_the_one_click_shortcuts()
    {
        var xaml = ReadView("TakeAttendancePage.xaml");

        xaml.Should().Contain(
            "<KeyBinding Key=\"S\" Modifiers=\"Control\" Command=\"{Binding SaveCommand}\" />");
        xaml.Should().Contain("<KeyBinding Key=\"F9\" Command=\"{Binding ScanQrCommand}\" />");
        xaml.Should().Contain(
            "<KeyBinding Key=\"D1\" Modifiers=\"Control\" Command=\"{Binding SetAllPresentCommand}\" />");
        xaml.Should().Contain(
            "<KeyBinding Key=\"D2\" Modifiers=\"Control\" Command=\"{Binding SetAllAbsentCommand}\" />");
        xaml.Should().Contain(
            "<KeyBinding Key=\"D3\" Modifiers=\"Control\" Command=\"{Binding SetAllLateCommand}\" />");
    }

    [Fact]
    public void The_homework_report_keeps_its_editor_shortcuts()
    {
        var xaml = ReadView("HomeworkReportPage.xaml");

        xaml.Should().Contain("Key=\"Escape\"");
        xaml.Should().Contain("Key=\"Enter\"");
        xaml.Should().Contain(
            "<KeyBinding Key=\"N\" Modifiers=\"Control\" Command=\"{Binding AddCommand}\" />");
    }

    [Fact]
    public void The_report_page_keeps_its_export_shortcuts()
    {
        var xaml = ReadView("AttendanceReportPage.xaml");

        xaml.Should().Contain(
            "<KeyBinding Key=\"P\" Modifiers=\"Control\" Command=\"{Binding PrintCommand}\" />");
        xaml.Should().Contain(
            "<KeyBinding Key=\"E\" Modifiers=\"Control\" Command=\"{Binding ExportExcelCommand}\" />");
    }

    [Fact]
    public void The_scanner_window_keeps_its_enter_and_escape_keys()
    {
        var xaml = ReadView("QrScannerWindow.xaml");

        xaml.Should().Contain(
            "<KeyBinding Key=\"Enter\" Command=\"{Binding SubmitInputCommand}\" />");
        xaml.Should().Contain(
            "<KeyBinding Key=\"Escape\" Command=\"{Binding CancelCommand}\" />");
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
