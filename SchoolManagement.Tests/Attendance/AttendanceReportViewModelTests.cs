using FluentAssertions;
using NSubstitute;
using SchoolManagement.Application.Attendance;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Students;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Tests.Attendance;

/// <summary>
/// Behaviour of the attendance report screen: the period/group/student filters,
/// the per-student counters and rate, and the PDF/Excel/print exports. The
/// report bundle is built by the real use case; the renderer and the file
/// dialogs are ports, so nothing touches QuestPDF or a printer here.
/// </summary>
public sealed class AttendanceReportViewModelTests
{
    private static readonly DateTime Day = AttendanceVmHarness.Day;

    [Fact]
    public async Task The_first_load_sends_the_current_month_and_builds_the_totals()
    {
        var harness = new AttendanceVmHarness();
        using var viewModel = harness.CreateReport();

        await viewModel.InitializeAsync();

        await harness.Attendance.Received(1).GetReportAsync(
            Arg.Is<AttendanceReportQuery>(query =>
                query.FromDate == new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)
                && query.ToDate == Day
                && query.GroupId == null
                && query.StudentId == null),
            Arg.Any<CancellationToken>());

        viewModel.HasRows.Should().BeTrue();
        viewModel.IsEmpty.Should().BeFalse();
        viewModel.Rows.Should().HaveCount(2);
        viewModel.Rows[0].StudentName.Should().Be("Alice Anderson");
        viewModel.Rows[1].StudentName.Should().Be("Bob Brown");
        viewModel.TotalStudentsText.Should().Be("2");
        viewModel.TotalRecordsText.Should().Be("8");
        viewModel.PresentText.Should().Be("5");
        viewModel.AbsentText.Should().Be("2");
        viewModel.LateText.Should().Be("1");
        viewModel.AttendanceRateText.Should().Be("75%");
        viewModel.PeriodText.Should().Be("2026-11-01 - 2026-11-04");
        viewModel.GroupText.Should().Be("reports.allStudents");
        viewModel.StudentText.Should().Be("reports.allStudents");
    }

    [Fact]
    public async Task The_filter_lists_start_with_an_all_entry()
    {
        var harness = new AttendanceVmHarness();
        using var viewModel = harness.CreateReport();

        await viewModel.InitializeAsync();

        viewModel.Groups.Should().HaveCount(2);
        viewModel.Groups[0].Id.Should().BeNull();
        viewModel.Groups[0].Label.Should().Be("homework.allGroups");
        viewModel.Groups[1].Should().Be(new StudentFilterOption(1, "Group Alpha"));

        viewModel.Students.Should().HaveCount(3);
        viewModel.Students[0].Id.Should().BeNull();
        viewModel.Students[0].Label.Should().Be("reports.allStudents");
        viewModel.Students[1].Should().Be(new StudentFilterOption(1, "Alice Anderson"));
        viewModel.Students[2].Should().Be(new StudentFilterOption(2, "Bob Brown"));
        await harness.Students.Received(1).GetListAsync(
            Arg.Is<StudentListQuery>(query => query.Take == 1000),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_group_and_student_filters_reach_the_query_and_the_header()
    {
        var harness = new AttendanceVmHarness();
        using var viewModel = harness.CreateReport();
        await viewModel.InitializeAsync();

        viewModel.SelectedGroup = viewModel.Groups[1];
        viewModel.SelectedStudent = viewModel.Students[1];

        var query = viewModel.CreateQuery();
        query.GroupId.Should().Be(1);
        query.StudentId.Should().Be(1);

        await viewModel.SearchCommand.ExecuteAsync(null);

        await harness.Attendance.Received().GetReportAsync(
            Arg.Is<AttendanceReportQuery>(candidate =>
                candidate.GroupId == 1 && candidate.StudentId == 1),
            Arg.Any<CancellationToken>());
        viewModel.GroupText.Should().Be("Group Alpha");
        viewModel.StudentText.Should().Be("Alice Anderson");
    }

    [Fact]
    public async Task An_inverted_period_is_refused_before_the_repository()
    {
        var harness = new AttendanceVmHarness();
        using var viewModel = harness.CreateReport();
        viewModel.FromDate = "2026-11-10";
        viewModel.ToDate = "2026-11-04";

        await viewModel.InitializeAsync();

        viewModel.ErrorMessage.Should().Be("validators.invalidDate");
        viewModel.Rows.Should().BeEmpty();
        await harness.Attendance.DidNotReceive().GetReportAsync(
            Arg.Any<AttendanceReportQuery>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_read_failure_clears_the_rows_and_shows_the_error()
    {
        var harness = new AttendanceVmHarness();
        harness.Attendance
            .GetReportAsync(Arg.Any<AttendanceReportQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<AttendanceReportRow>>.Fail(
                Failure.Database("errors.databaseFailure", "boom")));

        using var viewModel = harness.CreateReport();
        await viewModel.InitializeAsync();

        viewModel.ErrorMessage.Should().Be("errors.databaseFailure");
        viewModel.Rows.Should().BeEmpty();
        viewModel.HasRows.Should().BeFalse();
        viewModel.TotalRecordsText.Should().Be("0");
        viewModel.AttendanceRateText.Should().Be("0%");
    }

    [Fact]
    public async Task An_empty_report_is_flagged_without_dropping_the_period()
    {
        var harness = new AttendanceVmHarness();
        harness.Attendance
            .GetReportAsync(Arg.Any<AttendanceReportQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<AttendanceReportRow>>.Success(
                Array.Empty<AttendanceReportRow>()));

        using var viewModel = harness.CreateReport();
        await viewModel.InitializeAsync();

        viewModel.IsEmpty.Should().BeTrue();
        viewModel.HasRows.Should().BeFalse();
        viewModel.PeriodText.Should().Be("2026-11-01 - 2026-11-04");
    }

    [Fact]
    public async Task The_pdf_export_writes_the_rendered_bytes()
    {
        var harness = new AttendanceVmHarness();
        using var viewModel = harness.CreateReport();
        await viewModel.InitializeAsync();

        await viewModel.ExportPdfCommand.ExecuteAsync(null);

        await harness.ReportService.Received(1).BuildPdfAsync(
            Arg.Any<AttendanceReportData>(),
            Arg.Any<CancellationToken>());
        await harness.Files.Received(1).PickSavePathAsync(
            "reports.attendanceReport",
            "PDF (*.pdf)|*.pdf",
            "attendance-report.pdf");
        await harness.Files.Received(1).WriteAllBytesAsync(
            "C:\\temp\\attendance-report.pdf",
            Arg.Is<byte[]>(bytes => bytes.Length == 2));
        viewModel.StatusMessage.Should().Be("reports.exportSuccess");
        viewModel.IsExporting.Should().BeFalse();
    }

    [Fact]
    public async Task The_excel_export_uses_the_excel_filter_and_file_name()
    {
        var harness = new AttendanceVmHarness();
        harness.Files
            .PickSavePathAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns("C:\\temp\\attendance-report.xlsx");
        using var viewModel = harness.CreateReport();
        await viewModel.InitializeAsync();

        await viewModel.ExportExcelCommand.ExecuteAsync(null);

        await harness.ReportService.Received(1).BuildExcelAsync(
            Arg.Any<AttendanceReportData>(),
            Arg.Any<CancellationToken>());
        await harness.Files.Received(1).PickSavePathAsync(
            "reports.attendanceReport",
            "Excel (*.xlsx)|*.xlsx",
            "attendance-report.xlsx");
        await harness.Files.Received(1).WriteAllBytesAsync(
            "C:\\temp\\attendance-report.xlsx",
            Arg.Is<byte[]>(bytes => bytes.Length == 2));
        viewModel.StatusMessage.Should().Be("reports.exportSuccess");
    }

    [Fact]
    public async Task A_cancelled_save_dialog_writes_nothing()
    {
        var harness = new AttendanceVmHarness();
        harness.Files
            .PickSavePathAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns((string?)null);
        using var viewModel = harness.CreateReport();
        await viewModel.InitializeAsync();

        await viewModel.ExportPdfCommand.ExecuteAsync(null);

        await harness.Files.DidNotReceive().WriteAllBytesAsync(
            Arg.Any<string>(),
            Arg.Any<byte[]>());
        viewModel.StatusMessage.Should().BeEmpty();
    }

    [Fact]
    public async Task A_rendering_failure_is_shown_and_writes_nothing()
    {
        var harness = new AttendanceVmHarness();
        harness.ReportService
            .BuildPdfAsync(Arg.Any<AttendanceReportData>(), Arg.Any<CancellationToken>())
            .Returns(Result<byte[]>.Fail(Failure.Unexpected("reports.renderFailed", "boom")));
        using var viewModel = harness.CreateReport();
        await viewModel.InitializeAsync();

        await viewModel.ExportPdfCommand.ExecuteAsync(null);

        viewModel.ErrorMessage.Should().Be("reports.renderFailed");
        await harness.Files.DidNotReceive().PickSavePathAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>());
        viewModel.IsExporting.Should().BeFalse();
    }

    [Fact]
    public async Task Print_sends_the_pdf_to_the_printer_port()
    {
        var harness = new AttendanceVmHarness();
        using var viewModel = harness.CreateReport();
        await viewModel.InitializeAsync();

        await viewModel.PrintCommand.ExecuteAsync(null);

        await harness.Print.Received(1).PrintAsync(
            Arg.Is<byte[]>(bytes => bytes.Length == 2),
            "attendance-report.pdf");
        viewModel.IsExporting.Should().BeFalse();
    }

    [Fact]
    public async Task The_exports_and_the_print_are_no_ops_before_a_report_exists()
    {
        var harness = new AttendanceVmHarness();
        using var viewModel = harness.CreateReport();

        await viewModel.ExportPdfCommand.ExecuteAsync(null);
        await viewModel.ExportExcelCommand.ExecuteAsync(null);
        await viewModel.PrintCommand.ExecuteAsync(null);

        await harness.ReportService.DidNotReceive().BuildPdfAsync(
            Arg.Any<AttendanceReportData>(),
            Arg.Any<CancellationToken>());
        await harness.ReportService.DidNotReceive().BuildExcelAsync(
            Arg.Any<AttendanceReportData>(),
            Arg.Any<CancellationToken>());
        await harness.Print.DidNotReceive().PrintAsync(
            Arg.Any<byte[]>(),
            Arg.Any<string>());
    }

    [Fact]
    public async Task A_language_change_refreshes_the_title_and_the_filter_labels()
    {
        var harness = new AttendanceVmHarness();
        using var viewModel = harness.CreateReport();
        await viewModel.InitializeAsync();

        harness.Localization.Values["reports.attendanceReport"] = "Attendance report";
        harness.Localization.Values["homework.allGroups"] = "All groups";
        harness.Localization.Values["reports.allStudents"] = "All students";
        harness.Localization.RaiseLanguageChanged();

        viewModel.Title.Should().Be("Attendance report");
        viewModel.Groups[0].Label.Should().Be("All groups");
        viewModel.Students[0].Label.Should().Be("All students");
        viewModel.TotalRecordsText.Should().Be("8");
    }

    [Fact]
    public void Back_returns_to_the_attendance_list()
    {
        var harness = new AttendanceVmHarness();
        using var viewModel = harness.CreateReport();

        viewModel.BackCommand.Execute(null);

        harness.Navigation.Received(1).NavigateTo<AttendanceViewModel>();
    }
}
