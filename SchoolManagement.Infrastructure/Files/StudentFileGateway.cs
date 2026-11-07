using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Students;

namespace SchoolManagement.Infrastructure.Files;

/// <summary>
/// CSV and Excel transfer for the student screen. Everything is parsed into
/// <see cref="StudentExportRow"/> / <see cref="StudentImportRow"/> so the
/// application layer never depends on ClosedXML and the UI receives failures as
/// values, never exceptions.
/// </summary>
public sealed class StudentFileGateway : IStudentFileGateway
{
    private static readonly string[] Headers =
    {
        "FirstName",
        "LastName",
        "StudentCode",
        "DateOfBirth",
        "Gender",
        "Phone",
        "Address",
        "Status",
        "Notes",
        "RegistrationFee",
        "GuardianName",
        "GuardianPhone",
        "GuardianEmail",
        "GuardianRelationship",
    };

    private readonly ILogger<StudentFileGateway> _logger;

    public StudentFileGateway(ILogger<StudentFileGateway> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string DefaultExtension(StudentFileFormat format)
        => format == StudentFileFormat.Excel ? "xlsx" : "csv";

    public Task<Result<byte[]>> WriteAsync(
        StudentFileFormat format,
        IReadOnlyList<StudentExportRow> rows,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = format == StudentFileFormat.Excel
                ? WriteExcel(rows)
                : WriteCsv(rows);
            return Task.FromResult(Result<byte[]>.Success(content));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to write the student {Format} file", format);
            return Task.FromResult(Result<byte[]>.Fail(
                Failure.Unexpected(TranslationKeys.Errors.ExportFile)));
        }
    }

    public Task<Result<ParsedStudentFile>> ReadAsync(
        StudentFileFormat format,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rows = format == StudentFileFormat.Excel
                ? ReadExcel(content)
                : ReadCsv(content);
            return Task.FromResult(Result<ParsedStudentFile>.Success(rows));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to read the student {Format} file", format);
            return Task.FromResult(Result<ParsedStudentFile>.Fail(
                Failure.Unexpected(TranslationKeys.Errors.ImportFile)));
        }
    }

    private static byte[] WriteCsv(IReadOnlyList<StudentExportRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', Headers));

        foreach (var row in rows)
        {
            var cells = ExportCells(row).Select(EscapeCsv);
            builder.AppendLine(string.Join(',', cells));
        }

        var body = Encoding.UTF8.GetBytes(builder.ToString());
        var preamble = Encoding.UTF8.GetPreamble();
        var content = new byte[preamble.Length + body.Length];
        preamble.CopyTo(content, 0);
        body.CopyTo(content, preamble.Length);
        return content;
    }

    private static byte[] WriteExcel(IReadOnlyList<StudentExportRow> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Students");

        for (var column = 0; column < Headers.Length; column++)
        {
            sheet.Cell(1, column + 1).SetValue(Headers[column]);
            sheet.Cell(1, column + 1).Style.Font.Bold = true;
        }

        var rowIndex = 2;
        foreach (var row in rows)
        {
            var cells = ExportCells(row);
            for (var column = 0; column < cells.Length; column++)
            {
                sheet.Cell(rowIndex, column + 1).SetValue(cells[column]);
            }

            rowIndex++;
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string[] ExportCells(StudentExportRow row)
    {
        var (dob, gender, phone, address, notes) = (row.DateOfBirth, row.Gender, row.Phone, row.Address, row.Notes);
        return new[]
        {
            row.FirstName,
            row.LastName,
            row.StudentCode,
            dob is null ? string.Empty : dob.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            gender ?? string.Empty,
            phone ?? string.Empty,
            address ?? string.Empty,
            row.Status,
            notes ?? string.Empty,
            row.RegistrationFee.ToString(CultureInfo.InvariantCulture),
            row.GuardianName ?? string.Empty,
            row.GuardianPhone ?? string.Empty,
            row.GuardianEmail ?? string.Empty,
            row.GuardianRelationship ?? string.Empty,
        };
    }

    private static string EscapeCsv(string value)
    {
        if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static ParsedStudentFile ReadCsv(byte[] content)
    {
        var text = Encoding.UTF8.GetString(content).TrimStart('\uFEFF');
        var records = ParseCsv(text);
        if (records.Count == 0)
        {
            return new ParsedStudentFile(Array.Empty<StudentImportRow>(), Array.Empty<StudentImportError>());
        }

        var (headerMap, startIndex) = BuildHeaderMap(records[0]);
        var rows = new List<StudentImportRow>();
        var errors = new List<StudentImportError>();

        for (var index = startIndex; index < records.Count; index++)
        {
            var record = records[index];
            if (record.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            ParseRecord(record, headerMap, index + 1, rows, errors);
        }

        return new ParsedStudentFile(rows, errors);
    }

    private static ParsedStudentFile ReadExcel(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.FirstOrDefault();
        if (sheet is null)
        {
            return new ParsedStudentFile(Array.Empty<StudentImportRow>(), Array.Empty<StudentImportError>());
        }

        var rowNumbers = sheet.RowsUsed().Select(row => row.RowNumber()).ToList();
        if (rowNumbers.Count == 0)
        {
            return new ParsedStudentFile(Array.Empty<StudentImportRow>(), Array.Empty<StudentImportError>());
        }

        var headerRow = sheet.Row(rowNumbers[0]);
        var headerCells = Enumerable
            .Range(1, headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0)
            .Select(column => headerRow.Cell(column).GetString())
            .ToArray();
        var (headerMap, _) = BuildHeaderMap(headerCells);

        var rows = new List<StudentImportRow>();
        var errors = new List<StudentImportError>();

        foreach (var rowNumber in rowNumbers.Skip(1))
        {
            var row = sheet.Row(rowNumber);
            var width = Math.Max(headerCells.Length, row.LastCellUsed()?.Address.ColumnNumber ?? 0);
            var record = Enumerable
                .Range(1, Math.Max(width, Headers.Length))
                .Select(column => row.Cell(column).GetString())
                .ToArray();

            if (record.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            ParseRecord(record, headerMap, rowNumber, rows, errors);
        }

        return new ParsedStudentFile(rows, errors);
    }

    /// <summary>Maps a header row to column indexes, tolerating localized casing.</summary>
    private static (Dictionary<string, int> Map, int StartIndex) BuildHeaderMap(IReadOnlyList<string> first)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var column = 0; column < first.Count; column++)
        {
            var name = NormalizeHeader(first[column]);
            var canonical = Headers.FirstOrDefault(header =>
                string.Equals(NormalizeHeader(header), name, StringComparison.OrdinalIgnoreCase));
            if (canonical is not null)
            {
                map[canonical] = column;
            }
        }

        if (map.ContainsKey("FirstName") || map.ContainsKey("LastName"))
        {
            return (map, 1);
        }

        // No recognizable header: fall back to the canonical column order.
        map.Clear();
        for (var column = 0; column < Headers.Length && column < first.Count; column++)
        {
            map[Headers[column]] = column;
        }

        return (map, 0);
    }

    private static string NormalizeHeader(string value)
        => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static void ParseRecord(
        IReadOnlyList<string> record,
        IReadOnlyDictionary<string, int> headerMap,
        int rowNumber,
        List<StudentImportRow> rows,
        List<StudentImportError> errors)
    {
        var firstName = Read(record, headerMap, "FirstName") ?? string.Empty;
        var lastName = Read(record, headerMap, "LastName") ?? string.Empty;
        var reference = $"{firstName} {lastName}".Trim();

        DateTime? dateOfBirth = null;
        var rawDate = Read(record, headerMap, "DateOfBirth");
        if (!string.IsNullOrWhiteSpace(rawDate))
        {
            if (TryParseDate(rawDate, out var parsed))
            {
                dateOfBirth = parsed;
            }
            else
            {
                errors.Add(new StudentImportError(
                    rowNumber,
                    reference,
                    TranslationKeys.Validators.InvalidDate));
            }
        }

        var registrationFee = 0d;
        var rawFee = Read(record, headerMap, "RegistrationFee");
        if (!string.IsNullOrWhiteSpace(rawFee) && !TryParseNumber(rawFee, out registrationFee))
        {
            errors.Add(new StudentImportError(
                rowNumber,
                reference,
                TranslationKeys.Validators.InvalidNumber));
            registrationFee = 0;
        }

        rows.Add(new StudentImportRow(
            rowNumber,
            firstName,
            lastName,
            Blank(Read(record, headerMap, "StudentCode")),
            Blank(Read(record, headerMap, "Phone")),
            Blank(Read(record, headerMap, "GuardianName")),
            Blank(Read(record, headerMap, "GuardianPhone")),
            Blank(Read(record, headerMap, "Status")),
            registrationFee,
            dateOfBirth,
            Blank(Read(record, headerMap, "Gender")),
            Blank(Read(record, headerMap, "Address")),
            Blank(Read(record, headerMap, "Notes")),
            Blank(Read(record, headerMap, "GuardianEmail")),
            Blank(Read(record, headerMap, "GuardianRelationship"))));
    }

    private static string? Read(
        IReadOnlyList<string> record,
        IReadOnlyDictionary<string, int> headerMap,
        string name)
        => headerMap.TryGetValue(name, out var column) && column < record.Count
            ? record[column]
            : null;

    private static string? Blank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal static bool TryParseDate(string value, out DateTime date)
    {
        var formats = new[] { "yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy", "dd-MM-yyyy" };
        if (DateTime.TryParseExact(
                value.Trim(),
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var exact))
        {
            date = DateTime.SpecifyKind(exact.Date, DateTimeKind.Utc);
            return true;
        }

        if (DateTime.TryParse(
                value.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            date = DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
            return true;
        }

        date = default;
        return false;
    }

    internal static bool TryParseNumber(string value, out double number)
        => double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out number)
            || double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out number);

    /// <summary>RFC 4180 parser: quoted fields, escaped quotes and embedded newlines.</summary>
    private static List<List<string>> ParseCsv(string text)
    {
        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (inQuotes)
            {
                if (character == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(character);
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    record.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    record.Add(field.ToString());
                    field.Clear();
                    records.Add(record);
                    record = new List<string>();
                    break;
                default:
                    field.Append(character);
                    break;
            }
        }

        if (field.Length > 0 || record.Count > 0)
        {
            record.Add(field.ToString());
            records.Add(record);
        }

        return records;
    }
}
