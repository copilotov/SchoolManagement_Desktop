using FluentAssertions;
using NSubstitute;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Students;
using SchoolManagement.Application.UseCases;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Tests.Students;

/// <summary>
/// Phase 5 write/read use cases. The license gate must run before any write,
/// validation must short-circuit before the repository, and import failures must
/// stay per row instead of aborting the whole file.
/// </summary>
public sealed class StudentUseCaseTests
{
    private static ILicenseGuard Allowing()
    {
        var guard = Substitute.For<ILicenseGuard>();
        guard.EnsureCanWrite().Returns(Result<bool>.Success(true));
        return guard;
    }

    private static ILicenseGuard Blocking()
    {
        var guard = Substitute.For<ILicenseGuard>();
        guard.EnsureCanWrite().Returns(Result<bool>.Fail(
            Failure.License(TranslationKeys.License.ErrorWriteBlocked)));
        return guard;
    }

    private static IStudentRepository Repository() => Substitute.For<IStudentRepository>();

    private static StudentSaveRequest Request() => new(
        "Alice",
        "Anderson",
        null,
        new DateTime(2010, 5, 4, 0, 0, 0, DateTimeKind.Utc),
        "female",
        "0555 12 34 56",
        "  Street  ",
        StudentStatuses.Active,
        null,
        1000,
        "  Guardian One  ",
        "0661 12 34 56",
        "guardian@example.com",
        "mother");

    [Fact]
    public async Task Save_trims_normalizes_and_returns_the_generated_code()
    {
        var repository = Repository();
        var guard = Allowing();
        repository.CodeExistsAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));

        StudentSaveRequest? stored = null;
        repository
            .SaveAsync(Arg.Do<StudentSaveRequest>(request => stored = request), Arg.Any<CancellationToken>())
            .Returns(Result<StudentSaveOutcome>.Success(new StudentSaveOutcome(9, true, "STU-2026-0009")));

        var result = await new SaveStudentUseCase(repository, guard).ExecuteAsync(Request());

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.StudentCode.Should().Be("STU-2026-0009");
        stored.Should().NotBeNull();
        stored!.FirstName.Should().Be("Alice");
        stored.Address.Should().Be("Street");
        stored.Phone.Should().Be("+213555123456");
        stored.GuardianName.Should().Be("Guardian One");
        stored.GuardianPhone.Should().Be("+213661123456");
        guard.Received(1).EnsureCanWrite();
    }

    [Fact]
    public async Task Save_checks_a_supplied_code_before_the_repository()
    {
        var repository = Repository();
        repository.CodeExistsAsync("STU-2026-0001", null, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));
        repository.SaveAsync(Arg.Any<StudentSaveRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<StudentSaveOutcome>.Success(new StudentSaveOutcome(1, true, "STU-2026-0001")));

        var result = await new SaveStudentUseCase(repository, Allowing())
            .ExecuteAsync(Request() with { StudentCode = " STU-2026-0001 " });

        result.IsSuccess.Should().BeTrue();
        await repository.Received(1).CodeExistsAsync("STU-2026-0001", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_rejects_a_duplicate_code_without_writing()
    {
        var repository = Repository();
        repository.CodeExistsAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        var result = await new SaveStudentUseCase(repository, Allowing())
            .ExecuteAsync(Request() with { StudentCode = "STU-2026-0001" });

        result.IsFailure.Should().BeTrue();
        result.Failure!.MessageKey.Should().Be(TranslationKeys.Validators.CodeExists);
        await repository.DidNotReceive().SaveAsync(
            Arg.Any<StudentSaveRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_propagates_a_code_lookup_failure()
    {
        var repository = Repository();
        repository.CodeExistsAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Fail(Failure.Database()));

        var result = await new SaveStudentUseCase(repository, Allowing())
            .ExecuteAsync(Request() with { StudentCode = "STU-2026-0001" });

        result.Failure!.Kind.Should().Be(FailureKind.Database);
        await repository.DidNotReceive().SaveAsync(
            Arg.Any<StudentSaveRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_validation_short_circuits_before_the_repository()
    {
        var repository = Repository();

        var result = await new SaveStudentUseCase(repository, Allowing())
            .ExecuteAsync(Request() with { FirstName = "  " });

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Validators.Required);
        await repository.DidNotReceive().CodeExistsAsync(
            Arg.Any<string>(),
            Arg.Any<int?>(),
            Arg.Any<CancellationToken>());
        await repository.DidNotReceive().SaveAsync(
            Arg.Any<StudentSaveRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_blocked_save_never_reaches_the_repository()
    {
        var repository = Repository();
        var guard = Blocking();

        var result = await new SaveStudentUseCase(repository, guard).ExecuteAsync(Request());

        result.Failure!.Kind.Should().Be(FailureKind.License);
        guard.Received(1).EnsureCanWrite();
        await repository.DidNotReceive().SaveAsync(
            Arg.Any<StudentSaveRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_is_blocked_when_links_exist_and_reports_their_counts()
    {
        var repository = Repository();
        repository.GetDependenciesAsync(5, Arg.Any<CancellationToken>())
            .Returns(Result<StudentDependencies>.Success(new StudentDependencies(2, 3, 4)));

        var result = await new DeleteStudentUseCase(repository, Allowing()).ExecuteAsync(5);

        result.IsFailure.Should().BeTrue();
        result.Failure!.MessageKey.Should().Be(TranslationKeys.Students.DeleteBlocked);
        result.Failure.Parameters["enrollments"].Should().Be("2");
        result.Failure.Parameters["payments"].Should().Be("3");
        result.Failure.Parameters["attendance"].Should().Be("4");
        await repository.DidNotReceive().DeleteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_removes_a_student_without_links()
    {
        var repository = Repository();
        repository.GetDependenciesAsync(5, Arg.Any<CancellationToken>())
            .Returns(Result<StudentDependencies>.Success(new StudentDependencies(0, 0, 0)));
        repository.DeleteAsync(5, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        var result = await new DeleteStudentUseCase(repository, Allowing()).ExecuteAsync(5);

        result.IsSuccess.Should().BeTrue();
        await repository.Received(1).DeleteAsync(5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_blocked_delete_never_reads_the_dependencies()
    {
        var repository = Repository();
        var guard = Blocking();

        var result = await new DeleteStudentUseCase(repository, guard).ExecuteAsync(5);

        result.Failure!.Kind.Should().Be(FailureKind.License);
        guard.Received(1).EnsureCanWrite();
        await repository.DidNotReceive().GetDependenciesAsync(
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Import_reports_each_rejected_row_and_saves_the_valid_ones()
    {
        var gateway = Substitute.For<IStudentFileGateway>();
        var repository = Repository();
        var rows = new[]
        {
            new StudentImportRow(2, "  Alice  ", "Anderson", null, null, null, null, null, 0),
            new StudentImportRow(3, "Bob", "  ", null, null, null, null, null, 0),
            new StudentImportRow(4, "Carol", "Clark", "S001", null, null, null, null, 0),
        };
        gateway
            .ReadAsync(StudentFileFormat.Csv, Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Result<ParsedStudentFile>.Success(
                new ParsedStudentFile(rows, Array.Empty<StudentImportError>())));
        repository.CodeExistsAsync(Arg.Is<string>(code => code != "S001"), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));
        repository.CodeExistsAsync("S001", null, Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));
        repository.SaveAsync(Arg.Any<StudentSaveRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<StudentSaveOutcome>.Success(new StudentSaveOutcome(1, true, "STU-2026-0001")));

        var result = await new ImportStudentsUseCase(gateway, repository, Allowing())
            .ExecuteAsync(new StudentImportRequest(StudentFileFormat.Csv, new byte[] { 1 }));

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.Created.Should().Be(1);
        result.Value.Failed.Should().Be(2);
        result.Value.Errors.Select(error => error.RowNumber).Should().BeEquivalentTo(new[] { 3, 4 });
        result.Value.Errors.Should().Contain(error =>
            error.MessageKey == TranslationKeys.Validators.CodeExists);

        await repository.Received(1).SaveAsync(
            Arg.Is<StudentSaveRequest>(request => request.FirstName == "Alice"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Import_turns_a_save_failure_into_a_row_error()
    {
        var gateway = Substitute.For<IStudentFileGateway>();
        gateway
            .ReadAsync(StudentFileFormat.Excel, Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Result<ParsedStudentFile>.Success(new ParsedStudentFile(
                new[] { new StudentImportRow(2, "Alice", "Anderson", null, null, null, null, null, 0) },
                Array.Empty<StudentImportError>())));

        var repository = Repository();
        repository.SaveAsync(Arg.Any<StudentSaveRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<StudentSaveOutcome>.Fail(Failure.Database()));

        var result = await new ImportStudentsUseCase(gateway, repository, Allowing())
            .ExecuteAsync(new StudentImportRequest(StudentFileFormat.Excel, new byte[] { 1 }));

        result.Value.Created.Should().Be(0);
        result.Value.Errors.Should().ContainSingle()
            .Which.MessageKey.Should().Be(TranslationKeys.Errors.DatabaseFailure);
    }

    [Fact]
    public async Task Import_keeps_the_gateway_parse_errors()
    {
        var gateway = Substitute.For<IStudentFileGateway>();
        gateway
            .ReadAsync(Arg.Any<StudentFileFormat>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Result<ParsedStudentFile>.Success(new ParsedStudentFile(
                Array.Empty<StudentImportRow>(),
                new[] { new StudentImportError(7, "bad", TranslationKeys.Validators.InvalidDate) })));

        var result = await new ImportStudentsUseCase(gateway, Repository(), Allowing())
            .ExecuteAsync(new StudentImportRequest(StudentFileFormat.Csv, new byte[] { 1 }));

        result.Value.Created.Should().Be(0);
        result.Value.Errors.Should().ContainSingle()
            .Which.RowNumber.Should().Be(7);
    }

    [Fact]
    public async Task Import_rejects_an_empty_file_before_the_gateway()
    {
        var gateway = Substitute.For<IStudentFileGateway>();

        var result = await new ImportStudentsUseCase(gateway, Repository(), Allowing())
            .ExecuteAsync(new StudentImportRequest(StudentFileFormat.Csv, Array.Empty<byte>()));

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Students.ImportNoRows);
        await gateway.DidNotReceive().ReadAsync(
            Arg.Any<StudentFileFormat>(),
            Arg.Any<byte[]>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Import_propagates_a_gateway_failure()
    {
        var gateway = Substitute.For<IStudentFileGateway>();
        gateway
            .ReadAsync(Arg.Any<StudentFileFormat>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Result<ParsedStudentFile>.Fail(Failure.Unexpected(TranslationKeys.Errors.ImportFile)));

        var result = await new ImportStudentsUseCase(gateway, Repository(), Allowing())
            .ExecuteAsync(new StudentImportRequest(StudentFileFormat.Csv, new byte[] { 1 }));

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Errors.ImportFile);
    }

    [Fact]
    public async Task A_blocked_import_never_reads_the_file()
    {
        var gateway = Substitute.For<IStudentFileGateway>();
        var guard = Blocking();

        var result = await new ImportStudentsUseCase(gateway, Repository(), guard)
            .ExecuteAsync(new StudentImportRequest(StudentFileFormat.Csv, new byte[] { 1 }));

        result.Failure!.Kind.Should().Be(FailureKind.License);
        guard.Received(1).EnsureCanWrite();
        await gateway.DidNotReceive().ReadAsync(
            Arg.Any<StudentFileFormat>(),
            Arg.Any<byte[]>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Export_writes_the_repository_rows_through_the_gateway()
    {
        var repository = Repository();
        var gateway = Substitute.For<IStudentFileGateway>();
        var rows = new[] { ExportRow(1, "STU-2026-0001", "Alice") };
        repository.GetExportRowsAsync(Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StudentExportRow>>.Success(rows));
        gateway.DefaultExtension(StudentFileFormat.Csv).Returns("csv");
        gateway.WriteAsync(StudentFileFormat.Csv, rows, Arg.Any<CancellationToken>())
            .Returns(Result<byte[]>.Success(new byte[] { 1, 2, 3 }));

        var result = await new ExportStudentsUseCase(repository, gateway)
            .ExecuteAsync(StudentFileFormat.Csv);

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.Count.Should().Be(1);
        result.Value.Content.Should().Equal(1, 2, 3);
        result.Value.FileName.Should().StartWith("students-").And.EndWith(".csv");
    }

    [Fact]
    public async Task Export_refuses_an_empty_database()
    {
        var repository = Repository();
        repository.GetExportRowsAsync(Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StudentExportRow>>.Success(Array.Empty<StudentExportRow>()));
        var gateway = Substitute.For<IStudentFileGateway>();

        var result = await new ExportStudentsUseCase(repository, gateway)
            .ExecuteAsync(StudentFileFormat.Excel);

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Students.ExportEmpty);
        await gateway.DidNotReceive().WriteAsync(
            Arg.Any<StudentFileFormat>(),
            Arg.Any<IReadOnlyList<StudentExportRow>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Export_propagates_a_write_failure()
    {
        var repository = Repository();
        var gateway = Substitute.For<IStudentFileGateway>();
        repository.GetExportRowsAsync(Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StudentExportRow>>.Success(new[] { ExportRow(1, "A", "B") }));
        gateway.WriteAsync(Arg.Any<StudentFileFormat>(), Arg.Any<IReadOnlyList<StudentExportRow>>(), Arg.Any<CancellationToken>())
            .Returns(Result<byte[]>.Fail(Failure.Unexpected(TranslationKeys.Errors.ExportFile)));

        var result = await new ExportStudentsUseCase(repository, gateway)
            .ExecuteAsync(StudentFileFormat.Csv);

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Errors.ExportFile);
    }

    [Fact]
    public async Task Qr_rendering_requires_an_existing_student()
    {
        var repository = Repository();
        var cards = Substitute.For<IStudentCardService>();
        repository.GetCardDataAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StudentCardData>>.Success(Array.Empty<StudentCardData>()));

        var result = await new RenderStudentQrUseCase(repository, cards).ExecuteAsync(3);

        result.Failure!.Kind.Should().Be(FailureKind.NotFound);
        cards.DidNotReceive().RenderQrPng(Arg.Any<string>(), Arg.Any<int>());
    }

    [Fact]
    public async Task Qr_rendering_uses_the_student_code_as_payload()
    {
        var repository = Repository();
        var cards = Substitute.For<IStudentCardService>();
        repository.GetCardDataAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StudentCardData>>.Success(new[] { Card(3, "STU-2026-0003") }));
        cards.RenderQrPng("STU-2026-0003", Arg.Any<int>())
            .Returns(Result<byte[]>.Success(new byte[] { 9 }));

        var result = await new RenderStudentQrUseCase(repository, cards).ExecuteAsync(3);

        result.Value.Should().Equal(9);
        cards.Received(1).RenderQrPng("STU-2026-0003", Arg.Any<int>());
    }

    [Fact]
    public async Task The_bulk_card_pdf_requires_at_least_one_student()
    {
        var repository = Repository();
        var cards = Substitute.For<IStudentCardService>();
        repository.GetCardDataAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StudentCardData>>.Success(Array.Empty<StudentCardData>()));

        var result = await new BuildStudentsCardsPdfUseCase(repository, cards).ExecuteAsync(new[] { 1 });

        result.Failure!.Kind.Should().Be(FailureKind.NotFound);
    }

    [Fact]
    public async Task The_qr_sheet_requires_at_least_one_student()
    {
        var repository = Repository();
        var cards = Substitute.For<IStudentCardService>();
        repository.GetCardDataAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StudentCardData>>.Success(Array.Empty<StudentCardData>()));

        var result = await new BuildStudentsQrSheetPdfUseCase(repository, cards).ExecuteAsync(new[] { 1 });

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Students.QrEmpty);
    }

    private static StudentExportRow ExportRow(int id, string code, string first) => new(
        id,
        code,
        first,
        "Anderson",
        null,
        null,
        null,
        null,
        StudentStatuses.Active,
        null,
        0,
        null,
        null,
        null,
        null,
        null,
        null,
        null);

    private static StudentCardData Card(int id, string code) => new(
        id,
        code,
        "Alice Anderson",
        "Level One",
        "Grade One",
        "Group One",
        "Guardian One",
        "+213661123456",
        "Test School",
        "2026-2027");
}
