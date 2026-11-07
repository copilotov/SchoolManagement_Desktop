using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Students;
using SchoolManagement.Domain.Common;
using SchoolManagement.Infrastructure.Files;

namespace SchoolManagement.Tests.Students;

/// <summary>
/// CSV/Excel transfer of the student screen: the same columns are written and
/// read back, CSV is Excel-friendly (BOM, RFC 4180 quoting) and malformed cells
/// become per-row errors instead of an exception at the UI.
/// </summary>
public sealed class StudentFileGatewayTests
{
    private static StudentFileGateway Gateway() => new(NullLogger<StudentFileGateway>.Instance);

    private static StudentExportRow Row() => new(
        1,
        "STU-2026-0001",
        "Alice",
        "Anderson",
        new DateTime(2010, 5, 4, 0, 0, 0, DateTimeKind.Utc),
        "female",
        "+213555123456",
        "12 Rue des Ecoles, Bat A",
        StudentStatuses.Active,
        "Line one\nLine two \"quoted\"",
        1500.5,
        "Guardian One",
        "+213661123456",
        "guardian@example.com",
        "mother",
        "Level One",
        "Grade One",
        "Group One");

    [Theory]
    [InlineData(StudentFileFormat.Csv)]
    [InlineData(StudentFileFormat.Excel)]
    public async Task An_export_round_trips_through_the_read_path(StudentFileFormat format)
    {
        var gateway = Gateway();
        var written = await gateway.WriteAsync(format, new[] { Row() });
        written.IsSuccess.Should().BeTrue(written.Failure?.Detail);

        var read = await gateway.ReadAsync(format, written.Value);
        read.IsSuccess.Should().BeTrue(read.Failure?.Detail);
        read.Value.Errors.Should().BeEmpty();
        read.Value.Rows.Should().ContainSingle();

        var row = read.Value.Rows[0];
        row.FirstName.Should().Be("Alice");
        row.LastName.Should().Be("Anderson");
        row.StudentCode.Should().Be("STU-2026-0001");
        row.Phone.Should().Be("+213555123456");
        row.Address.Should().Be("12 Rue des Ecoles, Bat A");
        row.Gender.Should().Be("female");
        row.Status.Should().Be(StudentStatuses.Active);
        row.Notes.Should().Be("Line one\nLine two \"quoted\"");
        row.RegistrationFee.Should().Be(1500.5);
        row.GuardianName.Should().Be("Guardian One");
        row.GuardianEmail.Should().Be("guardian@example.com");
        row.GuardianRelationship.Should().Be("mother");
        row.DateOfBirth.Should().Be(new DateTime(2010, 5, 4, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Csv_starts_with_a_utf8_bom_and_the_canonical_headers()
    {
        var written = await Gateway().WriteAsync(StudentFileFormat.Csv, new[] { Row() });

        written.Value.Take(3).Should().Equal(Encoding.UTF8.GetPreamble());
        var text = Encoding.UTF8.GetString(written.Value).TrimStart('\uFEFF');
        text.Should().StartWith("FirstName,LastName,StudentCode,DateOfBirth,Gender,Phone,Address,Status,Notes,RegistrationFee,GuardianName,GuardianPhone,GuardianEmail,GuardianRelationship");
        text.Should().Contain("\"12 Rue des Ecoles, Bat A\"");
        text.Should().Contain("2010-05-04");
    }

    [Fact]
    public async Task A_headerless_csv_falls_back_to_the_canonical_order()
    {
        const string csv = "Dina,Diallo,STU-2026-0010,,,,,,,,,,,";

        var read = await Gateway().ReadAsync(StudentFileFormat.Csv, Encoding.UTF8.GetBytes(csv));

        read.IsSuccess.Should().BeTrue(read.Failure?.Detail);
        read.Value.Rows.Should().ContainSingle();
        read.Value.Rows[0].FirstName.Should().Be("Dina");
        read.Value.Rows[0].LastName.Should().Be("Diallo");
        read.Value.Rows[0].StudentCode.Should().Be("STU-2026-0010");
        read.Value.Rows[0].RowNumber.Should().Be(1);
    }

    [Fact]
    public async Task An_invalid_date_becomes_a_per_row_error()
    {
        const string csv = """
            FirstName,LastName,DateOfBirth
            Alice,Anderson,not-a-date
            Bob,Brown,2011-02-03
            """;

        var read = await Gateway().ReadAsync(StudentFileFormat.Csv, Encoding.UTF8.GetBytes(csv));

        read.IsSuccess.Should().BeTrue(read.Failure?.Detail);
        read.Value.Rows.Should().HaveCount(2);
        read.Value.Errors.Should().ContainSingle();
        read.Value.Errors[0].RowNumber.Should().Be(2);
        read.Value.Errors[0].Reference.Should().Be("Alice Anderson");
        read.Value.Errors[0].MessageKey.Should().Be(TranslationKeys.Validators.InvalidDate);
        read.Value.Rows[0].DateOfBirth.Should().BeNull();
        read.Value.Rows[1].DateOfBirth.Should().Be(new DateTime(2011, 2, 3, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task An_invalid_number_becomes_a_per_row_error_and_zero()
    {
        const string csv = """
            FirstName,LastName,RegistrationFee
            Alice,Anderson,many
            """;

        var read = await Gateway().ReadAsync(StudentFileFormat.Csv, Encoding.UTF8.GetBytes(csv));

        read.Value.Errors.Should().ContainSingle()
            .Which.MessageKey.Should().Be(TranslationKeys.Validators.InvalidNumber);
        read.Value.Rows.Should().ContainSingle().Which.RegistrationFee.Should().Be(0);
    }

    [Fact]
    public async Task Blank_lines_are_skipped()
    {
        const string csv = "FirstName,LastName\n\nAlice,Anderson\n,\n";

        var read = await Gateway().ReadAsync(StudentFileFormat.Csv, Encoding.UTF8.GetBytes(csv));

        read.Value.Rows.Should().ContainSingle().Which.FirstName.Should().Be("Alice");
        read.Value.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task An_empty_file_reads_as_no_rows()
    {
        var csv = await Gateway().ReadAsync(StudentFileFormat.Csv, Array.Empty<byte>());
        csv.Value.Rows.Should().BeEmpty();
        csv.Value.Errors.Should().BeEmpty();

        var headerOnly = await Gateway().ReadAsync(
            StudentFileFormat.Csv,
            Encoding.UTF8.GetBytes("FirstName,LastName\n"));
        headerOnly.Value.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task A_corrupt_excel_file_returns_a_failure_instead_of_throwing()
    {
        var read = await Gateway().ReadAsync(StudentFileFormat.Excel, new byte[] { 1, 2, 3, 4 });

        read.IsFailure.Should().BeTrue();
        read.Failure!.MessageKey.Should().Be(TranslationKeys.Errors.ImportFile);
    }

    [Theory]
    [InlineData(StudentFileFormat.Csv, "csv")]
    [InlineData(StudentFileFormat.Excel, "xlsx")]
    public void The_default_extension_matches_the_format(StudentFileFormat format, string extension)
    {
        Gateway().DefaultExtension(format).Should().Be(extension);
    }
}
