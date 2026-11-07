using System.Globalization;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Students;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.UseCases;

// Phase 5 read use cases -----------------------------------------------------

public sealed record StudentLookups(
    IReadOnlyList<LookupItem> Levels,
    IReadOnlyList<LookupItem> Grades,
    IReadOnlyList<LookupItem> Groups);

/// <summary>Small lookup bundle for the filter bar and the shared student form.</summary>
public sealed class GetStudentLookupsUseCase
{
    private readonly IReferenceDataRepository _references;
    private readonly IGroupRepository _groups;

    public GetStudentLookupsUseCase(IReferenceDataRepository references, IGroupRepository groups)
    {
        _references = references ?? throw new ArgumentNullException(nameof(references));
        _groups = groups ?? throw new ArgumentNullException(nameof(groups));
    }

    public async Task<Result<StudentLookups>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var levels = await _references.GetAcademicLevelsAsync(false, cancellationToken).ConfigureAwait(false);
        if (levels.IsFailure)
        {
            return Result<StudentLookups>.Fail(levels.Failure!);
        }

        var grades = await _references.GetGradesAsync(null, false, cancellationToken).ConfigureAwait(false);
        if (grades.IsFailure)
        {
            return Result<StudentLookups>.Fail(grades.Failure!);
        }

        var groups = await _groups.GetSummariesAsync(true, cancellationToken).ConfigureAwait(false);
        if (groups.IsFailure)
        {
            return Result<StudentLookups>.Fail(groups.Failure!);
        }

        return Result<StudentLookups>.Success(new StudentLookups(
            levels.Value,
            grades.Value,
            groups.Value.Select(group => new LookupItem(group.Id, group.Name)).ToArray()));
    }
}

public sealed class GetStudentsUseCase
{
    private readonly IStudentRepository _repository;

    public GetStudentsUseCase(IStudentRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<Result<IReadOnlyList<StudentListItem>>> ExecuteAsync(
        StudentListQuery query,
        CancellationToken cancellationToken = default)
        => _repository.GetListAsync(query, cancellationToken);

    public Task<Result<int>> CountAsync(
        StudentListQuery query,
        CancellationToken cancellationToken = default)
        => _repository.CountAsync(query, cancellationToken);
}

public sealed class GetStudentDetailsUseCase
{
    private readonly IStudentRepository _repository;

    public GetStudentDetailsUseCase(IStudentRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<Result<StudentDetails?>> ExecuteAsync(
        int studentId,
        CancellationToken cancellationToken = default)
        => _repository.GetDetailsAsync(studentId, cancellationToken);
}

public sealed record StudentActivity(
    IReadOnlyList<EnrollmentListItem> Enrollments,
    IReadOnlyList<PaymentListItem> Payments,
    IReadOnlyList<AttendanceListItem> Attendance,
    StudentAbsenceSummary AbsenceSummary);

/// <summary>One call that fills the three detail tabs without touching repositories.</summary>
public sealed class GetStudentActivityUseCase
{
    private static readonly DateTime FromDate = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ToDate = new(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly IEnrollmentRepository _enrollments;
    private readonly IPaymentRepository _payments;
    private readonly IAttendanceRepository _attendance;
    private readonly IStudentRepository _students;

    public GetStudentActivityUseCase(
        IEnrollmentRepository enrollments,
        IPaymentRepository payments,
        IAttendanceRepository attendance,
        IStudentRepository students)
    {
        _enrollments = enrollments ?? throw new ArgumentNullException(nameof(enrollments));
        _payments = payments ?? throw new ArgumentNullException(nameof(payments));
        _attendance = attendance ?? throw new ArgumentNullException(nameof(attendance));
        _students = students ?? throw new ArgumentNullException(nameof(students));
    }

    public async Task<Result<StudentActivity>> ExecuteAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var enrollments = await _enrollments.GetByStudentAsync(studentId, cancellationToken).ConfigureAwait(false);
        if (enrollments.IsFailure)
        {
            return Result<StudentActivity>.Fail(enrollments.Failure!);
        }

        var payments = await _payments.GetByStudentAsync(studentId, cancellationToken).ConfigureAwait(false);
        if (payments.IsFailure)
        {
            return Result<StudentActivity>.Fail(payments.Failure!);
        }

        var attendance = await _attendance
            .GetByStudentAsync(studentId, FromDate, ToDate, cancellationToken)
            .ConfigureAwait(false);
        if (attendance.IsFailure)
        {
            return Result<StudentActivity>.Fail(attendance.Failure!);
        }

        var summary = await _students.GetAbsenceSummaryAsync(studentId, cancellationToken).ConfigureAwait(false);
        if (summary.IsFailure)
        {
            return Result<StudentActivity>.Fail(summary.Failure!);
        }

        return Result<StudentActivity>.Success(new StudentActivity(
            enrollments.Value,
            payments.Value,
            attendance.Value,
            summary.Value));
    }
}

public sealed class GetStudentCardDataUseCase
{
    private readonly IStudentRepository _repository;

    public GetStudentCardDataUseCase(IStudentRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<Result<IReadOnlyList<StudentCardData>>> ExecuteAsync(
        IReadOnlyCollection<int> studentIds,
        CancellationToken cancellationToken = default)
        => _repository.GetCardDataAsync(studentIds, cancellationToken);
}

// Phase 5 guarded writes -----------------------------------------------------

[WriteOperation]
public sealed class SaveStudentUseCase : WriteUseCase<StudentSaveRequest, StudentSaveOutcome>
{
    private readonly IStudentRepository _repository;

    public SaveStudentUseCase(IStudentRepository repository, ILicenseGuard guard)
        : base(guard)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    protected override async Task<Result<StudentSaveOutcome>> ExecuteCoreAsync(
        StudentSaveRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var failure = StudentValidator.Validate(request);
        if (failure is not null)
        {
            return Result<StudentSaveOutcome>.Fail(failure);
        }

        var code = request.StudentCode?.Trim();
        if (!string.IsNullOrEmpty(code))
        {
            var exists = await _repository
                .CodeExistsAsync(code, request.StudentId, cancellationToken)
                .ConfigureAwait(false);
            if (exists.IsFailure)
            {
                return Result<StudentSaveOutcome>.Fail(exists.Failure!);
            }

            if (exists.Value)
            {
                return Result<StudentSaveOutcome>.Fail(
                    Failure.Validation(TranslationKeys.Validators.CodeExists, field: "studentCode"));
            }
        }

        var normalized = Normalize(request);
        return await _repository.SaveAsync(normalized, cancellationToken).ConfigureAwait(false);
    }

    internal static StudentSaveRequest Normalize(StudentSaveRequest request) => request with
    {
        FirstName = request.FirstName.Trim(),
        LastName = request.LastName.Trim(),
        StudentCode = string.IsNullOrWhiteSpace(request.StudentCode) ? null : request.StudentCode.Trim(),
        Gender = string.IsNullOrWhiteSpace(request.Gender) ? null : request.Gender.Trim().ToLowerInvariant(),
        Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : AlgeriaPhone.Normalize(request.Phone),
        Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
        Status = string.IsNullOrWhiteSpace(request.Status) ? StudentStatuses.Active : request.Status.Trim(),
        Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
        GuardianName = string.IsNullOrWhiteSpace(request.GuardianName) ? null : request.GuardianName.Trim(),
        GuardianPhone = string.IsNullOrWhiteSpace(request.GuardianPhone)
            ? null
            : AlgeriaPhone.Normalize(request.GuardianPhone),
        GuardianEmail = string.IsNullOrWhiteSpace(request.GuardianEmail) ? null : request.GuardianEmail.Trim(),
        GuardianRelationship = string.IsNullOrWhiteSpace(request.GuardianRelationship)
            ? null
            : request.GuardianRelationship.Trim(),
    };
}

[WriteOperation]
public sealed class DeleteStudentUseCase : WriteUseCase<int, bool>
{
    private readonly IStudentRepository _repository;

    public DeleteStudentUseCase(IStudentRepository repository, ILicenseGuard guard)
        : base(guard)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    protected override async Task<Result<bool>> ExecuteCoreAsync(
        int request,
        CancellationToken cancellationToken)
    {
        var dependencies = await _repository.GetDependenciesAsync(request, cancellationToken).ConfigureAwait(false);
        if (dependencies.IsFailure)
        {
            return Result<bool>.Fail(dependencies.Failure!);
        }

        if (dependencies.Value.HasLinks)
        {
            return Result<bool>.Fail(Failure.Validation(
                TranslationKeys.Students.DeleteBlocked,
                parameters: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["enrollments"] = dependencies.Value.Enrollments.ToString(CultureInfo.InvariantCulture),
                    ["payments"] = dependencies.Value.Payments.ToString(CultureInfo.InvariantCulture),
                    ["attendance"] = dependencies.Value.Attendance.ToString(CultureInfo.InvariantCulture),
                }));
        }

        return await _repository.DeleteAsync(request, cancellationToken).ConfigureAwait(false);
    }
}

[WriteOperation]
public sealed class ImportStudentsUseCase : WriteUseCase<StudentImportRequest, StudentImportResult>
{
    private readonly IStudentFileGateway _gateway;
    private readonly IStudentRepository _repository;

    public ImportStudentsUseCase(IStudentFileGateway gateway, IStudentRepository repository, ILicenseGuard guard)
        : base(guard)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    protected override async Task<Result<StudentImportResult>> ExecuteCoreAsync(
        StudentImportRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Content is null || request.Content.Length == 0)
        {
            return Result<StudentImportResult>.Fail(
                Failure.Validation(TranslationKeys.Students.ImportNoRows));
        }

        var parsed = await _gateway
            .ReadAsync(request.Format, request.Content, cancellationToken)
            .ConfigureAwait(false);
        if (parsed.IsFailure)
        {
            return Result<StudentImportResult>.Fail(parsed.Failure!);
        }

        var errors = new List<StudentImportError>(parsed.Value.Errors);
        var created = 0;

        foreach (var row in parsed.Value.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var saveRequest = new StudentSaveRequest(
                row.FirstName,
                row.LastName,
                row.StudentCode,
                row.DateOfBirth,
                row.Gender,
                row.Phone,
                row.Address,
                row.Status ?? StudentStatuses.Active,
                row.Notes,
                row.RegistrationFee,
                row.GuardianName,
                row.GuardianPhone,
                row.GuardianEmail,
                row.GuardianRelationship);

            var validation = StudentValidator.Validate(saveRequest);
            if (validation is not null)
            {
                errors.Add(new StudentImportError(
                    row.RowNumber,
                    Reference(row),
                    validation.MessageKey,
                    validation.Parameters));
                continue;
            }

            var code = saveRequest.StudentCode?.Trim();
            if (!string.IsNullOrEmpty(code))
            {
                var exists = await _repository.CodeExistsAsync(code, null, cancellationToken).ConfigureAwait(false);
                if (exists.IsFailure)
                {
                    return Result<StudentImportResult>.Fail(exists.Failure!);
                }

                if (exists.Value)
                {
                    errors.Add(new StudentImportError(
                        row.RowNumber,
                        Reference(row),
                        TranslationKeys.Validators.CodeExists));
                    continue;
                }
            }

            var save = await _repository
                .SaveAsync(SaveStudentUseCase.Normalize(saveRequest), cancellationToken)
                .ConfigureAwait(false);

            if (save.IsFailure)
            {
                errors.Add(new StudentImportError(
                    row.RowNumber,
                    Reference(row),
                    save.Failure!.MessageKey,
                    save.Failure.Parameters));
                continue;
            }

            created++;
        }

        return Result<StudentImportResult>.Success(new StudentImportResult(created, errors));
    }

    private static string Reference(StudentImportRow row)
        => $"{row.FirstName} {row.LastName}".Trim();
}

public sealed class ExportStudentsUseCase
{
    private readonly IStudentRepository _repository;
    private readonly IStudentFileGateway _gateway;

    public ExportStudentsUseCase(IStudentRepository repository, IStudentFileGateway gateway)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    }

    public async Task<Result<StudentExportResult>> ExecuteAsync(
        StudentFileFormat format,
        CancellationToken cancellationToken = default)
    {
        var rows = await _repository.GetExportRowsAsync(cancellationToken).ConfigureAwait(false);
        if (rows.IsFailure)
        {
            return Result<StudentExportResult>.Fail(rows.Failure!);
        }

        if (rows.Value.Count == 0)
        {
            return Result<StudentExportResult>.Fail(Failure.Validation(TranslationKeys.Students.ExportEmpty));
        }

        var written = await _gateway.WriteAsync(format, rows.Value, cancellationToken).ConfigureAwait(false);
        if (written.IsFailure)
        {
            return Result<StudentExportResult>.Fail(written.Failure!);
        }

        var fileName = string.Create(
            CultureInfo.InvariantCulture,
            $"students-{DateTime.UtcNow:yyyyMMdd-HHmmss}.{_gateway.DefaultExtension(format)}");

        return Result<StudentExportResult>.Success(
            new StudentExportResult(fileName, written.Value, rows.Value.Count));
    }
}

// Read-only rendering use cases: the ViewModel never references QuestPDF/ZXing.

public sealed class RenderStudentQrUseCase
{
    private readonly IStudentRepository _repository;
    private readonly IStudentCardService _cards;

    public RenderStudentQrUseCase(IStudentRepository repository, IStudentCardService cards)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _cards = cards ?? throw new ArgumentNullException(nameof(cards));
    }

    public async Task<Result<byte[]>> ExecuteAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var data = await StudentCardLoader.LoadAsync(_repository, studentId, cancellationToken).ConfigureAwait(false);
        return data.IsFailure ? Result<byte[]>.Fail(data.Failure!) : _cards.RenderQrPng(data.Value.StudentCode);
    }
}

/// <summary>
/// Renders every QR of the bulk page with a single card-data query, so the
/// bulk screen never fires one repository round-trip per student.
/// </summary>
public sealed class RenderStudentsQrUseCase
{
    private readonly IStudentRepository _repository;
    private readonly IStudentCardService _cards;

    public RenderStudentsQrUseCase(IStudentRepository repository, IStudentCardService cards)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _cards = cards ?? throw new ArgumentNullException(nameof(cards));
    }

    public async Task<Result<IReadOnlyList<StudentQrItem>>> ExecuteAsync(
        IReadOnlyCollection<int> studentIds,
        CancellationToken cancellationToken = default)
    {
        var data = await _repository.GetCardDataAsync(studentIds, cancellationToken).ConfigureAwait(false);
        if (data.IsFailure)
        {
            return Result<IReadOnlyList<StudentQrItem>>.Fail(data.Failure!);
        }

        var items = new List<StudentQrItem>(data.Value.Count);
        foreach (var card in data.Value)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var qr = _cards.RenderQrPng(card.StudentCode, 320);
            if (qr.IsFailure)
            {
                return Result<IReadOnlyList<StudentQrItem>>.Fail(qr.Failure!);
            }

            items.Add(new StudentQrItem(card.StudentId, card.StudentCode, card.FullName, qr.Value));
        }

        return Result<IReadOnlyList<StudentQrItem>>.Success(items);
    }
}

public sealed class RenderStudentCardPreviewUseCase
{
    private readonly IStudentRepository _repository;
    private readonly IStudentCardService _cards;

    public RenderStudentCardPreviewUseCase(IStudentRepository repository, IStudentCardService cards)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _cards = cards ?? throw new ArgumentNullException(nameof(cards));
    }

    public async Task<Result<byte[]>> ExecuteAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var data = await StudentCardLoader.LoadAsync(_repository, studentId, cancellationToken).ConfigureAwait(false);
        return data.IsFailure ? Result<byte[]>.Fail(data.Failure!) : _cards.RenderCardPreviewPng(data.Value);
    }
}

public sealed class BuildStudentCardPdfUseCase
{
    private readonly IStudentRepository _repository;
    private readonly IStudentCardService _cards;

    public BuildStudentCardPdfUseCase(IStudentRepository repository, IStudentCardService cards)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _cards = cards ?? throw new ArgumentNullException(nameof(cards));
    }

    public async Task<Result<byte[]>> ExecuteAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var data = await StudentCardLoader.LoadAsync(_repository, studentId, cancellationToken).ConfigureAwait(false);
        return data.IsFailure ? Result<byte[]>.Fail(data.Failure!) : _cards.RenderCardPdf(data.Value);
    }
}

/// <summary>Builds the A4 QR sheet used by the bulk print screen.</summary>
public sealed class BuildStudentsQrSheetPdfUseCase
{
    private readonly IStudentRepository _repository;
    private readonly IStudentCardService _cards;

    public BuildStudentsQrSheetPdfUseCase(IStudentRepository repository, IStudentCardService cards)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _cards = cards ?? throw new ArgumentNullException(nameof(cards));
    }

    public async Task<Result<byte[]>> ExecuteAsync(
        IReadOnlyCollection<int> studentIds,
        CancellationToken cancellationToken = default)
    {
        var data = await _repository.GetCardDataAsync(studentIds, cancellationToken).ConfigureAwait(false);
        if (data.IsFailure)
        {
            return Result<byte[]>.Fail(data.Failure!);
        }

        var items = new List<StudentQrItem>(data.Value.Count);
        foreach (var card in data.Value)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var qr = _cards.RenderQrPng(card.StudentCode, 320);
            if (qr.IsFailure)
            {
                return Result<byte[]>.Fail(qr.Failure!);
            }

            items.Add(new StudentQrItem(card.StudentId, card.StudentCode, card.FullName, qr.Value));
        }

        return items.Count == 0
            ? Result<byte[]>.Fail(Failure.Validation(TranslationKeys.Students.QrEmpty))
            : _cards.RenderQrSheetPdf(items);
    }
}

public sealed class BuildStudentsCardsPdfUseCase{
    private readonly IStudentRepository _repository;
    private readonly IStudentCardService _cards;

    public BuildStudentsCardsPdfUseCase(IStudentRepository repository, IStudentCardService cards)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _cards = cards ?? throw new ArgumentNullException(nameof(cards));
    }

    public async Task<Result<byte[]>> ExecuteAsync(
        IReadOnlyCollection<int> studentIds,
        CancellationToken cancellationToken = default)
    {
        var data = await _repository.GetCardDataAsync(studentIds, cancellationToken).ConfigureAwait(false);
        if (data.IsFailure)
        {
            return Result<byte[]>.Fail(data.Failure!);
        }

        return data.Value.Count == 0
            ? Result<byte[]>.Fail(Failure.NotFound(TranslationKeys.Errors.NotFound))
            : _cards.RenderCardsPdf(data.Value);
    }
}

internal static class StudentCardLoader
{
    internal static async Task<Result<StudentCardData>> LoadAsync(
        IStudentRepository repository,
        int studentId,
        CancellationToken cancellationToken)
    {
        var data = await repository
            .GetCardDataAsync(new[] { studentId }, cancellationToken)
            .ConfigureAwait(false);
        if (data.IsFailure)
        {
            return Result<StudentCardData>.Fail(data.Failure!);
        }

        var card = data.Value.FirstOrDefault();
        return card is null
            ? Result<StudentCardData>.Fail(Failure.NotFound(TranslationKeys.Errors.NotFound))
            : Result<StudentCardData>.Success(card);
    }
}
