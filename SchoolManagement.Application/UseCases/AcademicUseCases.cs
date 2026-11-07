using System.Globalization;
using SchoolManagement.Application.Academic;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;

namespace SchoolManagement.Application.UseCases;

// Read use cases -------------------------------------------------------------

public sealed class GetAcademicLevelsUseCase
{
    private readonly IAcademicLevelRepository _repository;

    public GetAcademicLevelsUseCase(IAcademicLevelRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<Result<IReadOnlyList<AcademicLevelListItem>>> ExecuteAsync(
        AcademicListQuery query,
        CancellationToken cancellationToken = default)
        => _repository.GetListAsync(query, cancellationToken);
}

public sealed class GetGradesUseCase
{
    private readonly IGradeRepository _repository;

    public GetGradesUseCase(IGradeRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<Result<IReadOnlyList<GradeListItem>>> ExecuteAsync(
        AcademicListQuery query,
        CancellationToken cancellationToken = default)
        => _repository.GetListAsync(query, cancellationToken);
}

public sealed class GetSubjectsUseCase
{
    private readonly ISubjectRepository _repository;

    public GetSubjectsUseCase(ISubjectRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<Result<IReadOnlyList<SubjectListItem>>> ExecuteAsync(
        AcademicListQuery query,
        CancellationToken cancellationToken = default)
        => _repository.GetListAsync(query, cancellationToken);
}

public sealed class GetTeachersUseCase
{
    private readonly ITeacherRepository _repository;

    public GetTeachersUseCase(ITeacherRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<Result<IReadOnlyList<TeacherListItem>>> ExecuteAsync(
        AcademicListQuery query,
        CancellationToken cancellationToken = default)
        => _repository.GetListAsync(query, cancellationToken);
}

public sealed class GetGradeSubjectsUseCase
{
    private readonly IGradeRepository _repository;

    public GetGradeSubjectsUseCase(IGradeRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<Result<IReadOnlyList<int>>> ExecuteAsync(
        int gradeId,
        CancellationToken cancellationToken = default)
        => _repository.GetSubjectIdsAsync(gradeId, cancellationToken);
}

// Academic level writes ------------------------------------------------------

public sealed record AcademicLevelUpdateRequest(int Id, AcademicLevelInput Input);

[WriteOperation]
public sealed class AddAcademicLevelUseCase : WriteUseCase<AcademicLevelInput, int>
{
    private readonly IAcademicLevelRepository _repository;

    public AddAcademicLevelUseCase(IAcademicLevelRepository repository, ILicenseGuard guard)
        : base(guard)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    protected override Task<Result<int>> ExecuteCoreAsync(
        AcademicLevelInput request,
        CancellationToken cancellationToken)
    {
        var failure = AcademicValidator.ValidateLevel(request);
        return failure is not null
            ? Task.FromResult(Result<int>.Fail(failure))
            : _repository.AddAsync(AcademicValidator.ToEntity(request), cancellationToken);
    }
}

[WriteOperation]
public sealed class UpdateAcademicLevelUseCase : WriteUseCase<AcademicLevelUpdateRequest, bool>
{
    private readonly IAcademicLevelRepository _repository;

    public UpdateAcademicLevelUseCase(IAcademicLevelRepository repository, ILicenseGuard guard)
        : base(guard)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    protected override async Task<Result<bool>> ExecuteCoreAsync(
        AcademicLevelUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var failure = AcademicValidator.ValidateLevel(request.Input);
        if (failure is not null)
        {
            return Result<bool>.Fail(failure);
        }

        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (existing.IsFailure)
        {
            return Result<bool>.Fail(existing.Failure!);
        }

        if (existing.Value is null)
        {
            return Result<bool>.Fail(Failure.NotFound(TranslationKeys.Errors.NotFound));
        }

        var entity = existing.Value;
        entity.Name = request.Input.Name.Trim();
        entity.OrderIndex = request.Input.OrderIndex;
        entity.IsActive = request.Input.IsActive;

        return await _repository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
    }
}

[WriteOperation]
public sealed class DeleteAcademicLevelUseCase : WriteUseCase<int, bool>
{
    private readonly IAcademicLevelRepository _repository;

    public DeleteAcademicLevelUseCase(IAcademicLevelRepository repository, ILicenseGuard guard)
        : base(guard)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    protected override async Task<Result<bool>> ExecuteCoreAsync(
        int request,
        CancellationToken cancellationToken)
    {
        var grades = await _repository.CountGradesAsync(request, cancellationToken).ConfigureAwait(false);
        if (grades.IsFailure)
        {
            return Result<bool>.Fail(grades.Failure!);
        }

        if (grades.Value > 0)
        {
            return Result<bool>.Fail(Blocked(TranslationKeys.Levels.DeleteBlockedGrades, grades.Value));
        }

        var groups = await _repository.CountGroupsAsync(request, cancellationToken).ConfigureAwait(false);
        if (groups.IsFailure)
        {
            return Result<bool>.Fail(groups.Failure!);
        }

        if (groups.Value > 0)
        {
            return Result<bool>.Fail(Blocked(TranslationKeys.Levels.DeleteBlockedGroups, groups.Value));
        }

        return await _repository.DeleteAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static Failure Blocked(string key, int count) =>
        Failure.Validation(
            key,
            parameters: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["count"] = count.ToString(CultureInfo.InvariantCulture),
            });
}

// Grade writes ---------------------------------------------------------------

public sealed record AcademicGradeUpdateRequest(int Id, AcademicGradeInput Input);

public sealed record GradeSubjectAssignment(int GradeId, IReadOnlyCollection<int> SubjectIds);

[WriteOperation]
public sealed class AddGradeUseCase : WriteUseCase<AcademicGradeInput, int>
{
    private readonly IGradeRepository _repository;

    public AddGradeUseCase(IGradeRepository repository, ILicenseGuard guard)
        : base(guard)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    protected override Task<Result<int>> ExecuteCoreAsync(
        AcademicGradeInput request,
        CancellationToken cancellationToken)
    {
        var failure = AcademicValidator.ValidateGrade(request);
        return failure is not null
            ? Task.FromResult(Result<int>.Fail(failure))
            : _repository.AddAsync(AcademicValidator.ToEntity(request), cancellationToken);
    }
}

[WriteOperation]
public sealed class UpdateGradeUseCase : WriteUseCase<AcademicGradeUpdateRequest, bool>
{
    private readonly IGradeRepository _repository;

    public UpdateGradeUseCase(IGradeRepository repository, ILicenseGuard guard)
        : base(guard)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    protected override async Task<Result<bool>> ExecuteCoreAsync(
        AcademicGradeUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var failure = AcademicValidator.ValidateGrade(request.Input);
        if (failure is not null)
        {
            return Result<bool>.Fail(failure);
        }

        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (existing.IsFailure)
        {
            return Result<bool>.Fail(existing.Failure!);
        }

        if (existing.Value is null)
        {
            return Result<bool>.Fail(Failure.NotFound(TranslationKeys.Errors.NotFound));
        }

        var entity = existing.Value;
        entity.Name = request.Input.Name.Trim();
        entity.AcademicLevelId = request.Input.AcademicLevelId;
        entity.OrderIndex = request.Input.OrderIndex;
        entity.IsActive = request.Input.IsActive;

        return await _repository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
    }
}

[WriteOperation]
public sealed class DeleteGradeUseCase : WriteUseCase<int, bool>
{
    private readonly IGradeRepository _repository;

    public DeleteGradeUseCase(IGradeRepository repository, ILicenseGuard guard)
        : base(guard)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    protected override async Task<Result<bool>> ExecuteCoreAsync(
        int request,
        CancellationToken cancellationToken)
    {
        var groups = await _repository.CountGroupsAsync(request, cancellationToken).ConfigureAwait(false);
        if (groups.IsFailure)
        {
            return Result<bool>.Fail(groups.Failure!);
        }

        if (groups.Value > 0)
        {
            return Result<bool>.Fail(
                Failure.Validation(
                    TranslationKeys.Grades.DeleteBlockedGroups,
                    parameters: new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["count"] = groups.Value.ToString(CultureInfo.InvariantCulture),
                    }));
        }

        return await _repository.DeleteAsync(request, cancellationToken).ConfigureAwait(false);
    }
}

[WriteOperation]
public sealed class SetGradeSubjectsUseCase : WriteUseCase<GradeSubjectAssignment, bool>
{
    private readonly IGradeRepository _repository;

    public SetGradeSubjectsUseCase(IGradeRepository repository, ILicenseGuard guard)
        : base(guard)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    protected override Task<Result<bool>> ExecuteCoreAsync(
        GradeSubjectAssignment request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var subjectIds = request.SubjectIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        return _repository.SetSubjectsAsync(request.GradeId, subjectIds, cancellationToken);
    }
}

// Subject writes -------------------------------------------------------------

public sealed record AcademicSubjectUpdateRequest(int Id, AcademicSubjectInput Input);

[WriteOperation]
public sealed class AddSubjectUseCase : WriteUseCase<AcademicSubjectInput, int>
{
    private readonly ISubjectRepository _repository;

    public AddSubjectUseCase(ISubjectRepository repository, ILicenseGuard guard)
        : base(guard)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    protected override async Task<Result<int>> ExecuteCoreAsync(
        AcademicSubjectInput request,
        CancellationToken cancellationToken)
    {
        var failure = AcademicValidator.ValidateSubject(request);
        if (failure is not null)
        {
            return Result<int>.Fail(failure);
        }

        var codeFailure = await EnsureCodeIsFreeAsync(request.Code, null, cancellationToken)
            .ConfigureAwait(false);
        if (codeFailure is not null)
        {
            return Result<int>.Fail(codeFailure);
        }

        return await _repository
            .AddAsync(AcademicValidator.ToEntity(request), cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Failure?> EnsureCodeIsFreeAsync(
        string? code,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var exists = await _repository
            .CodeExistsAsync(code.Trim(), excludeId, cancellationToken)
            .ConfigureAwait(false);

        if (exists.IsFailure)
        {
            return exists.Failure;
        }

        return exists.Value
            ? Failure.Validation(TranslationKeys.Validators.CodeExists, field: "code")
            : null;
    }
}

[WriteOperation]
public sealed class UpdateSubjectUseCase : WriteUseCase<AcademicSubjectUpdateRequest, bool>
{
    private readonly ISubjectRepository _repository;

    public UpdateSubjectUseCase(ISubjectRepository repository, ILicenseGuard guard)
        : base(guard)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    protected override async Task<Result<bool>> ExecuteCoreAsync(
        AcademicSubjectUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var failure = AcademicValidator.ValidateSubject(request.Input);
        if (failure is not null)
        {
            return Result<bool>.Fail(failure);
        }

        if (!string.IsNullOrWhiteSpace(request.Input.Code))
        {
            var codeExists = await _repository
                .CodeExistsAsync(request.Input.Code.Trim(), request.Id, cancellationToken)
                .ConfigureAwait(false);

            if (codeExists.IsFailure)
            {
                return Result<bool>.Fail(codeExists.Failure!);
            }

            if (codeExists.Value)
            {
                return Result<bool>.Fail(
                    Failure.Validation(TranslationKeys.Validators.CodeExists, field: "code"));
            }
        }

        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (existing.IsFailure)
        {
            return Result<bool>.Fail(existing.Failure!);
        }

        if (existing.Value is null)
        {
            return Result<bool>.Fail(Failure.NotFound(TranslationKeys.Errors.NotFound));
        }

        var entity = existing.Value;
        entity.Name = request.Input.Name.Trim();
        entity.Code = string.IsNullOrWhiteSpace(request.Input.Code) ? null : request.Input.Code.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Input.Description)
            ? null
            : request.Input.Description.Trim();
        entity.IsActive = request.Input.IsActive;

        return await _repository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
    }
}

[WriteOperation]
public sealed class DeleteSubjectUseCase : WriteUseCase<int, bool>
{
    private readonly ISubjectRepository _repository;

    public DeleteSubjectUseCase(ISubjectRepository repository, ILicenseGuard guard)
        : base(guard)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    protected override async Task<Result<bool>> ExecuteCoreAsync(
        int request,
        CancellationToken cancellationToken)
    {
        var groups = await _repository.CountGroupsAsync(request, cancellationToken).ConfigureAwait(false);
        if (groups.IsFailure)
        {
            return Result<bool>.Fail(groups.Failure!);
        }

        if (groups.Value > 0)
        {
            return Result<bool>.Fail(Blocked(TranslationKeys.Subjects.DeleteBlockedGroups, groups.Value));
        }

        var grades = await _repository.CountGradesAsync(request, cancellationToken).ConfigureAwait(false);
        if (grades.IsFailure)
        {
            return Result<bool>.Fail(grades.Failure!);
        }

        if (grades.Value > 0)
        {
            return Result<bool>.Fail(Blocked(TranslationKeys.Subjects.DeleteBlockedGrades, grades.Value));
        }

        return await _repository.DeleteAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static Failure Blocked(string key, int count) =>
        Failure.Validation(
            key,
            parameters: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["count"] = count.ToString(CultureInfo.InvariantCulture),
            });
}

// Teacher writes -------------------------------------------------------------

public sealed record AcademicTeacherUpdateRequest(int Id, AcademicTeacherInput Input);

[WriteOperation]
public sealed class AddTeacherUseCase : WriteUseCase<AcademicTeacherInput, int>
{
    private readonly ITeacherRepository _repository;

    public AddTeacherUseCase(ITeacherRepository repository, ILicenseGuard guard)
        : base(guard)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    protected override Task<Result<int>> ExecuteCoreAsync(
        AcademicTeacherInput request,
        CancellationToken cancellationToken)
    {
        var failure = AcademicValidator.ValidateTeacher(request);
        return failure is not null
            ? Task.FromResult(Result<int>.Fail(failure))
            : _repository.AddAsync(AcademicValidator.ToEntity(request), cancellationToken);
    }
}

[WriteOperation]
public sealed class UpdateTeacherUseCase : WriteUseCase<AcademicTeacherUpdateRequest, bool>
{
    private readonly ITeacherRepository _repository;

    public UpdateTeacherUseCase(ITeacherRepository repository, ILicenseGuard guard)
        : base(guard)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    protected override async Task<Result<bool>> ExecuteCoreAsync(
        AcademicTeacherUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var failure = AcademicValidator.ValidateTeacher(request.Input);
        if (failure is not null)
        {
            return Result<bool>.Fail(failure);
        }

        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (existing.IsFailure)
        {
            return Result<bool>.Fail(existing.Failure!);
        }

        if (existing.Value is null)
        {
            return Result<bool>.Fail(Failure.NotFound(TranslationKeys.Errors.NotFound));
        }

        var entity = existing.Value;
        var input = request.Input;
        entity.FullName = input.FullName.Trim();
        entity.Phone = string.IsNullOrWhiteSpace(input.Phone) ? null : input.Phone.Trim();
        entity.Email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim();
        entity.Address = string.IsNullOrWhiteSpace(input.Address) ? null : input.Address.Trim();
        entity.Specialization = string.IsNullOrWhiteSpace(input.Specialization)
            ? null
            : input.Specialization.Trim();
        entity.HireDate = input.HireDate;
        entity.IsActive = input.IsActive;

        return await _repository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
    }
}

[WriteOperation]
public sealed class DeleteTeacherUseCase : WriteUseCase<int, bool>
{
    private readonly ITeacherRepository _repository;

    public DeleteTeacherUseCase(ITeacherRepository repository, ILicenseGuard guard)
        : base(guard)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    protected override async Task<Result<bool>> ExecuteCoreAsync(
        int request,
        CancellationToken cancellationToken)
    {
        var groups = await _repository.CountGroupsAsync(request, cancellationToken).ConfigureAwait(false);
        if (groups.IsFailure)
        {
            return Result<bool>.Fail(groups.Failure!);
        }

        if (groups.Value > 0)
        {
            return Result<bool>.Fail(
                Failure.Validation(
                    TranslationKeys.Teachers.DeleteBlockedGroups,
                    parameters: new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["count"] = groups.Value.ToString(CultureInfo.InvariantCulture),
                    }));
        }

        return await _repository.DeleteAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
