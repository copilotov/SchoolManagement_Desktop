using System.Globalization;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolManagement.Application.Attendance;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories.Models;

namespace SchoolManagement.Infrastructure.Files;

/// <summary>
/// Attendance period report in PDF and Excel. QuestPDF and ClosedXML stay behind
/// this Infrastructure port; Application only builds the immutable
/// <see cref="AttendanceReportData"/>, so the ViewModel receives bytes and stays
/// testable.
/// </summary>
public sealed class AttendanceReportService : IAttendanceReportService
{
    private static readonly object InitGate = new();
    private static bool _questPdfConfigured;

    private readonly ILocalizationService _localization;
    private readonly ILogger<AttendanceReportService> _logger;

    public AttendanceReportService(
        ILocalizationService localization,
        ILogger<AttendanceReportService> logger)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<Result<byte[]>> BuildPdfAsync(
        AttendanceReportData data,
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
                    EnsureQuestPdfConfigured();
                    page.Size(PageSizes.A4);
                    page.Margin(24);
                    page.DefaultTextStyle(style => style.FontFamily("Segoe UI").FontSize(9));

                    ApplyDirection(page.Content()).Column(column =>
                    {
                        ComposeHeader(column, data);
                        ComposeTable(column, data);
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
            _logger.LogError(exception, "Failed to build the attendance report PDF.");
            return Task.FromResult(Result<byte[]>.Fail(
                Failure.Unexpected(TranslationKeys.Errors.CardRender, exception.Message)));
        }
    }

    public Task<Result<byte[]>> BuildExcelAsync(
        AttendanceReportData data,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add(_localization.Tr(TranslationKeys.Reports.AttendanceReportTitle));

            sheet.Cell(1, 1).SetValue(data.SchoolName);
            sheet.Cell(1, 1).Style.Font.Bold = true;
            sheet.Cell(1, 1).Style.Font.FontSize = 14;

            sheet.Cell(2, 1).SetValue(_localization.Tr(TranslationKeys.Reports.AttendanceReportTitle));
            sheet.Cell(3, 1).SetValue($"{_localization.Tr(TranslationKeys.Reports.FromDate)}: {data.FromDate:yyyy-MM-dd}");
            sheet.Cell(3, 4).SetValue($"{_localization.Tr(TranslationKeys.Reports.ToDate)}: {data.ToDate:yyyy-MM-dd}");
            sheet.Cell(4, 1).SetValue(
                _localization.Tr(
                    TranslationKeys.Reports.GeneratedAt,
                    ("date", data.GeneratedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))));

            var headers = new[]
            {
                "#",
                _localization.Tr(TranslationKeys.Students.StudentCode),
                _localization.Tr(TranslationKeys.Students.FullName),
                _localization.Tr(TranslationKeys.Reports.FilterGroup),
                _localization.Tr(TranslationKeys.Reports.TotalSessions),
                _localization.Tr(TranslationKeys.Reports.PresentCount),
                _localization.Tr(TranslationKeys.Reports.AbsentCount),
                _localization.Tr(TranslationKeys.Reports.LateCount),
                _localization.Tr(TranslationKeys.Reports.AttendanceRate),
            };

            const int headerRow = 6;
            for (var column = 0; column < headers.Length; column++)
            {
                sheet.Cell(headerRow, column + 1).SetValue(headers[column]);
                sheet.Cell(headerRow, column + 1).Style.Font.Bold = true;
                sheet.Cell(headerRow, column + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
            }

            var rowIndex = headerRow + 1;
            var sequence = 1;
            foreach (var row in data.Rows)
            {
                sheet.Cell(rowIndex, 1).SetValue(sequence);
                sheet.Cell(rowIndex, 2).SetValue(row.StudentCode);
                sheet.Cell(rowIndex, 3).SetValue(row.StudentName);
                sheet.Cell(rowIndex, 4).SetValue(row.GroupName);
                sheet.Cell(rowIndex, 5).SetValue(row.SessionCount);
                sheet.Cell(rowIndex, 6).SetValue(row.PresentCount);
                sheet.Cell(rowIndex, 7).SetValue(row.AbsentCount);
                sheet.Cell(rowIndex, 8).SetValue(row.LateCount);
                sheet.Cell(rowIndex, 9).SetValue(row.AttendanceRate);
                sequence++;
                rowIndex++;
            }

            sheet.Cell(rowIndex, 1).SetValue(_localization.Tr(TranslationKeys.Reports.Summary));
            sheet.Cell(rowIndex, 1).Style.Font.Bold = true;
            sheet.Cell(rowIndex, 5).SetValue(data.TotalRecords);
            sheet.Cell(rowIndex, 6).SetValue(data.TotalPresent);
            sheet.Cell(rowIndex, 7).SetValue(data.TotalAbsent);
            sheet.Cell(rowIndex, 8).SetValue(data.TotalLate);
            sheet.Cell(rowIndex, 9).SetValue(data.AttendanceRate);

            if (data.Rows.Count == 0)
            {
                sheet.Cell(rowIndex + 2, 1).SetValue(_localization.Tr(TranslationKeys.Reports.NoRecords));
            }

            sheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return Task.FromResult(Result<byte[]>.Success(stream.ToArray()));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to build the attendance report Excel file.");
            return Task.FromResult(Result<byte[]>.Fail(
                Failure.Unexpected(TranslationKeys.Errors.ExportFile, exception.Message)));
        }
    }

    private IContainer ApplyDirection(IContainer container)
        => _localization.IsRightToLeft
            ? container.ContentFromRightToLeft()
            : container.ContentFromLeftToRight();

    private void ComposeHeader(ColumnDescriptor column, AttendanceReportData data)
    {
        column.Item().Row(row =>
        {
            row.RelativeItem().Column(header =>
            {
                header.Item().Text(data.SchoolName).Bold().FontSize(14);
                header.Item()
                    .Text(_localization.Tr(TranslationKeys.Reports.AttendanceReportTitle))
                    .FontSize(11)
                    .FontColor(Colors.Grey.Darken2);
                header.Item().Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(9));
                    text.Span($"{_localization.Tr(TranslationKeys.Reports.FromDate)}: {data.FromDate:yyyy-MM-dd}    ");
                    text.Span($"{_localization.Tr(TranslationKeys.Reports.ToDate)}: {data.ToDate:yyyy-MM-dd}");
                    if (!string.IsNullOrWhiteSpace(data.GroupName))
                    {
                        text.Span($"    {_localization.Tr(TranslationKeys.Reports.FilterGroup)}: {data.GroupName}");
                    }

                    if (!string.IsNullOrWhiteSpace(data.StudentName))
                    {
                        text.Span($"    {_localization.Tr(TranslationKeys.Reports.FilterStudent)}: {data.StudentName}");
                    }
                });
            });

            row.ConstantItem(130)
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

        column.Item().PaddingTop(6).Background(Colors.Grey.Lighten4).Padding(8).Text(text =>
        {
            text.DefaultTextStyle(style => style.FontSize(9));
            text.Span($"{_localization.Tr(TranslationKeys.Reports.TotalStudents)}: {data.StudentCount}    ");
            text.Span($"{_localization.Tr(TranslationKeys.Reports.TotalRecords)}: {data.TotalRecords}    ");
            text.Span($"{_localization.Tr(TranslationKeys.Reports.PresentCount)}: {data.TotalPresent}    ");
            text.Span($"{_localization.Tr(TranslationKeys.Reports.AbsentCount)}: {data.TotalAbsent}    ");
            text.Span($"{_localization.Tr(TranslationKeys.Reports.LateCount)}: {data.TotalLate}    ");
            text.Span($"{_localization.Tr(TranslationKeys.Reports.AttendanceRate)}: {data.AttendanceRate.ToString("0.#", CultureInfo.InvariantCulture)}%");
        });
    }

    private void ComposeTable(ColumnDescriptor column, AttendanceReportData data)
    {
        column.Item().PaddingTop(10).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(22);
                columns.ConstantColumn(62);
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.ConstantColumn(42);
                columns.ConstantColumn(40);
                columns.ConstantColumn(40);
                columns.ConstantColumn(38);
                columns.ConstantColumn(46);
            });

            table.Header(header =>
            {
                HeaderCell(header, " # ");
                HeaderCell(header, _localization.Tr(TranslationKeys.Students.StudentCode));
                HeaderCell(header, _localization.Tr(TranslationKeys.Students.FullName));
                HeaderCell(header, _localization.Tr(TranslationKeys.Reports.FilterGroup));
                HeaderCell(header, _localization.Tr(TranslationKeys.Reports.TotalSessions));
                HeaderCell(header, _localization.Tr(TranslationKeys.Reports.PresentCount));
                HeaderCell(header, _localization.Tr(TranslationKeys.Reports.AbsentCount));
                HeaderCell(header, _localization.Tr(TranslationKeys.Reports.LateCount));
                HeaderCell(header, _localization.Tr(TranslationKeys.Reports.AttendanceRate));
            });

            var index = 1;
            foreach (var row in data.Rows)
            {
                table.Cell().Element(BodyCell).Text(index.ToString(CultureInfo.InvariantCulture));
                table.Cell().Element(BodyCell).Text(row.StudentCode);
                table.Cell().Element(BodyCell).Text(row.StudentName);
                table.Cell().Element(BodyCell).Text(row.GroupName);
                table.Cell().Element(BodyCell).AlignRight().Text(row.SessionCount.ToString(CultureInfo.InvariantCulture));
                table.Cell().Element(BodyCell).AlignRight().Text(row.PresentCount.ToString(CultureInfo.InvariantCulture));
                table.Cell().Element(BodyCell).AlignRight().Text(row.AbsentCount.ToString(CultureInfo.InvariantCulture));
                table.Cell().Element(BodyCell).AlignRight().Text(row.LateCount.ToString(CultureInfo.InvariantCulture));
                table.Cell().Element(BodyCell)
                    .AlignRight()
                    .Text(row.AttendanceRate.ToString("0.#", CultureInfo.InvariantCulture) + "%");
                index++;
            }
        });

        if (data.Rows.Count == 0)
        {
            column.Item().PaddingTop(8)
                .Text(_localization.Tr(TranslationKeys.Reports.NoRecords))
                .FontSize(9)
                .FontColor(Colors.Grey.Darken2);
        }
    }

    private void ComposeFooter(ColumnDescriptor column, AttendanceReportData data)
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
