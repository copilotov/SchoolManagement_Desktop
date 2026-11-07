using SchoolManagement.Application.Common;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.UseCases;

[WriteOperation]
public sealed class AddStudentUseCase : WriteUseCase<int>
{
    private readonly IStudentRepository _repository;
    private readonly Student _student;

    public AddStudentUseCase(IStudentRepository repository, ILicenseGuard guard, Student student)
        : base(guard)
    {
        _repository = repository;
        _student = student;
    }

    protected override Task<Result<int>> ExecuteCoreAsync(CancellationToken cancellationToken) =>
        _repository.AddAsync(_student, cancellationToken);
}

[WriteOperation]
public sealed class UpdateStudentUseCase : WriteUseCase<bool>
{
    private readonly IStudentRepository _repository;
    private readonly Student _student;

    public UpdateStudentUseCase(IStudentRepository repository, ILicenseGuard guard, Student student)
        : base(guard)
    {
        _repository = repository;
        _student = student;
    }

    protected override Task<Result<bool>> ExecuteCoreAsync(CancellationToken cancellationToken) =>
        _repository.UpdateAsync(_student, cancellationToken);
}

[WriteOperation]
public sealed class AddGroupUseCase : WriteUseCase<int>
{
    private readonly IGroupRepository _repository;
    private readonly Group _group;

    public AddGroupUseCase(IGroupRepository repository, ILicenseGuard guard, Group group)
        : base(guard)
    {
        _repository = repository;
        _group = group;
    }

    protected override Task<Result<int>> ExecuteCoreAsync(CancellationToken cancellationToken) =>
        _repository.AddAsync(_group, cancellationToken);
}

[WriteOperation]
public sealed class UpdateGroupUseCase : WriteUseCase<bool>
{
    private readonly IGroupRepository _repository;
    private readonly Group _group;

    public UpdateGroupUseCase(IGroupRepository repository, ILicenseGuard guard, Group group)
        : base(guard)
    {
        _repository = repository;
        _group = group;
    }

    protected override Task<Result<bool>> ExecuteCoreAsync(CancellationToken cancellationToken) =>
        _repository.UpdateAsync(_group, cancellationToken);
}

[WriteOperation]
public sealed class DeleteGroupUseCase : WriteUseCase<bool>
{
    private readonly IGroupRepository _repository;
    private readonly int _groupId;

    public DeleteGroupUseCase(IGroupRepository repository, ILicenseGuard guard, int groupId)
        : base(guard)
    {
        _repository = repository;
        _groupId = groupId;
    }

    protected override Task<Result<bool>> ExecuteCoreAsync(CancellationToken cancellationToken) =>
        _repository.DeleteAsync(_groupId, cancellationToken);
}

[WriteOperation]
public sealed class EnrollStudentUseCase : WriteUseCase<int>
{
    private readonly IEnrollmentRepository _repository;
    private readonly Enrollment _enrollment;

    public EnrollStudentUseCase(IEnrollmentRepository repository, ILicenseGuard guard, Enrollment enrollment)
        : base(guard)
    {
        _repository = repository;
        _enrollment = enrollment;
    }

    protected override Task<Result<int>> ExecuteCoreAsync(CancellationToken cancellationToken) =>
        _repository.AddAsync(_enrollment, cancellationToken);
}
