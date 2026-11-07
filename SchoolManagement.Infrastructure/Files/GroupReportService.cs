using System.Globalization;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Groups;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories.Models;

namespace SchoolManagement.Infrastructure.Files;

/// <summary>
/// Printable full group report. QuestPDF stays behind this Infrastructure port:
/// Application builds the immutable <see cref="GroupReportData"/> and the
/// desktop only receives PDF bytes, so Domain/Application never reference a
/// rendering library and the ViewModel stays testable.
/// </summary>
public sealed class GroupReportService : IGroupReportService
{
    private static readonly object InitGate = new();
    private static bool _questPdfConfigured;

    private readonly ILocalizationService _localization;
    private readonly ILogger<GroupReportService> _logger;

    public GroupReportService(
        ILocalizationService localization,
        ILogger<GroupReportService> logger)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<Result<byte[]>> BuildFullReportAsync(
        GroupReportData data,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    ConfigurePage(page);
                    ApplyDirection(page.Content()).Column(column =>
                    {
                        ComposeHeader(column, data);
                        ComposeSummary(column, data);
                        ComposeAttendance(column, data);
                        ComposeFinance(column, data);
                        ComposeStudents(column, data);
                        ComposeFooter(column, data);
                    });
                });
            });

            return Task.FromResult(Result<byte[]>.Success(document.GeneratePdf()));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to build the full group report for group {GroupId}", data.Group.Id);
            return Task.FromResult(Result<byte[]>.Fail(
                Failure.Unexpected(TranslationKeys.Errors.CardRender)));
        }
    }

    private void ConfigurePage(PageDescriptor page)
    {
        EnsureQuestPdfConfigured();
        page.Size(PageSizes.A4);
        page.Margin(24);
        page.DefaultTextStyle(style => style.FontFamily("Segoe UI").FontSize(9));
    }

    private IContainer ApplyDirection(IContainer container)
        => _localization.IsRightToLeft
            ? container.ContentFromRightToLeft()
            : container.ContentFromLeftToRight();

    private void ComposeHeader(ColumnDescriptor column, GroupReportData data)
    {
        column.Item().Row(row =>
        {
            row.RelativeItem().Column(header =>
            {
                header.Item().Text(data.SchoolName).Bold().FontSize(14);
                header.Item()
                    .Text(_localization.Tr(
                        TranslationKeys.Reports.GroupReportTitle,
                        ("name", data.Group.Name)))
                    .FontSize(11)
                    .FontColor(Colors.Grey.Darken2);
                if (!string.IsNullOrWhiteSpace(data.AcademicYear))
                {
                    header.Item().Text(data.AcademicYear!).FontSize(9);
                }
            });

            row.ConstantItem(120)
                .AlignRight()
                .Column(meta =>
                {
                    meta.Item()
                        .Text(_localization.Tr(TranslationKeys.Reports.PrintDate))
                        .FontSize(8)
                        .FontColor(Colors.Grey.Darken2);
                    meta.Item()
                        .Text(data.GeneratedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
                        .FontSize(9);
                });
        });

        column.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
    }

    private void ComposeSummary(ColumnDescriptor column, GroupReportData data)
    {
        column.Item().PaddingTop(8).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            AddSummaryCell(table, TranslationKeys.Groups.Subject, data.Group.SubjectName ?? "-");
            AddSummaryCell(table, TranslationKeys.Groups.Teacher, data.Group.TeacherName ?? "-");
            AddSummaryCell(table, TranslationKeys.Groups.Grade, data.Group.GradeName ?? "-");
            AddSummaryCell(
                table,
                TranslationKeys.Groups.Capacity,
                $"{data.Group.ActiveEnrollments}/{data.Group.MaxStudents}");
            AddSummaryCell(
                table,
                TranslationKeys.Groups.MonthlyFee,
                data.Group.MonthlyFee.ToString("0.##", CultureInfo.InvariantCulture));
            AddSummaryCell(
                table,
                TranslationKeys.Groups.EnrolledStudents,
                data.Students.Count.ToString(CultureInfo.InvariantCulture));
            AddSummaryCell(
                table,
                TranslationKeys.Reports.SessionsCount,
                data.Attendance.SessionCount.ToString(CultureInfo.InvariantCulture));
        });

        column.Item().PaddingTop(10).Text(text =>
        {
            text.Span(_localization.Tr(TranslationKeys.Groups.StudentsCount)).Bold().FontSize(10);
        });
    }

    private void AddSummaryCell(TableDescriptor table, string labelKey, string value)
    {
        table.Cell().PaddingBottom(4).Column(cell =>
        {
            cell.Item()
                .Text(_localization.Tr(labelKey))
                .FontSize(8)
                .FontColor(Colors.Grey.Darken2);
            cell.Item().Text(value).FontSize(10).Bold();
        });
    }

    private void ComposeAttendance(ColumnDescriptor column, GroupReportData data)
    {
        column.Item().PaddingTop(6).Background(Colors.Grey.Lighten4).Padding(8).Column(box =>
        {
            box.Item()
                .Text(_localization.Tr(TranslationKeys.Reports.AttendanceSummary))
                .Bold()
                .FontSize(10);
            box.Item().PaddingTop(3).Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(9));
                text.Span($"{_localization.Tr(TranslationKeys.Reports.TotalSessions)}: {data.Attendance.SessionCount}    ");
                text.Span($"{_localization.Tr(TranslationKeys.Reports.PresentCount)}: {data.Attendance.PresentCount}    ");
                text.Span($"{_localization.Tr(TranslationKeys.Reports.AbsentCount)}: {data.Attendance.AbsentCount}    ");
                text.Span($"{_localization.Tr(TranslationKeys.Reports.LateCount)}: {data.Attendance.LateCount}    ");
                text.Span($"{_localization.Tr(TranslationKeys.Reports.AttendanceRate)}: {data.Attendance.AttendanceRate.ToString("0.#", CultureInfo.InvariantCulture)}%");
            });
        });
    }

    private void ComposeFinance(ColumnDescriptor column, GroupReportData data)
    {
        column.Item().PaddingTop(6).Background(Colors.Grey.Lighten4).Padding(8).Column(box =>
        {
            box.Item()
                .Text(_localization.Tr(TranslationKeys.Reports.FinancialSummary))
                .Bold()
                .FontSize(10);
            box.Item().PaddingTop(3).Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(9));
                text.Span($"{_localization.Tr(TranslationKeys.Reports.PeriodLabel)}: {data.MonthYear}    ");
                text.Span($"{_localization.Tr(TranslationKeys.Reports.TotalAmount)}: {data.TotalExpected.ToString("0.##", CultureInfo.InvariantCulture)}    ");
                text.Span($"{_localization.Tr(TranslationKeys.Reports.PaidLabel)}: {data.TotalPaid.ToString("0.##", CultureInfo.InvariantCulture)}    ");
                text.Span($"{_localization.Tr(TranslationKeys.Reports.RemainingLabel)}: {data.TotalOutstanding.ToString("0.##", CultureInfo.InvariantCulture)}");
            });
        });
    }

    private void ComposeStudents(ColumnDescriptor column, GroupReportData data)
    {
        column.Item().PaddingTop(10).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(22);
                columns.ConstantColumn(62);
                columns.RelativeColumn();
                columns.ConstantColumn(54);
                columns.ConstantColumn(60);
            });

            table.Header(header =>
            {
                HeaderCell(header, " # ");
                HeaderCell(header, _localization.Tr(TranslationKeys.Students.StudentCode));
                HeaderCell(header, _localization.Tr(TranslationKeys.Students.FullName));
                HeaderCell(header, _localization.Tr(TranslationKeys.Students.Status));
                HeaderCell(header, _localization.Tr(TranslationKeys.Reports.RemainingLabel));
            });

            var index = 1;
            foreach (var student in data.Students)
            {
                table.Cell().Element(BodyCell).Text(index.ToString(CultureInfo.InvariantCulture));
                table.Cell().Element(BodyCell).Text(student.StudentCode);
                table.Cell().Element(BodyCell).Text(student.FullName);
                table.Cell().Element(BodyCell).Text(StatusLabel(student.Status));
                table.Cell().Element(BodyCell)
                    .AlignRight()
                    .Text(student.Outstanding.ToString("0.##", CultureInfo.InvariantCulture));
                index++;
            }
        });

        if (data.Students.Count == 0)
        {
            column.Item().PaddingTop(8)
                .Text(_localization.Tr(TranslationKeys.Reports.NoDataPeriod))
                .FontSize(9)
                .FontColor(Colors.Grey.Darken2);
        }
    }

    private static void HeaderCell(TableCellDescriptor header, string text)
        => header.Cell()
            .Background(Colors.Grey.Lighten3)
            .Padding(4)
            .Text(text)
            .FontSize(8)
            .Bold();

    private static IContainer BodyCell(IContainer container)
        => container.BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten3)
            .Padding(4)
            .DefaultTextStyle(style => style.FontSize(8));

    private void ComposeFooter(ColumnDescriptor column, GroupReportData data)
    {
        column.Item().PaddingTop(16).Row(row =>
        {
            row.RelativeItem()
                .Text(_localization.Tr(TranslationKeys.Reports.ManagerSignature))
                .FontSize(9);
            row.RelativeItem()
                .AlignRight()
                .Text(data.GeneratedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .FontSize(9);
        });
    }

    private string StatusLabel(string status) => status switch
    {
        "active" => _localization.Tr(TranslationKeys.Students.Active),
        "inactive" => _localization.Tr(TranslationKeys.Students.Inactive),
        "withdrawn" => _localization.Tr(TranslationKeys.Students.Withdrawn),
        _ => status,
    };

    private static void EnsureQuestPdfConfigured()
    {
        if (_questPdfConfigured)
        {
            return;
        }

        lock (InitGate)
        {
            if (_questPdfConfigured)
            {
                return;
            }

            QuestPDF.Settings.License = LicenseType.Community;
            QuestPDF.Settings.UseEnvironmentFonts = true;
            QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
            _questPdfConfigured = true;
        }
    }
}
