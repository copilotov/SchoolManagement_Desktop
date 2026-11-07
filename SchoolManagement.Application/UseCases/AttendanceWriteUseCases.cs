using SchoolManagement.Application.Common;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.UseCases;

[WriteOperation]
public sealed class AddSessionUseCase : WriteUseCase<int>
{
    private readonly ISessionRepository _repository;
    private readonly Session _session;

    public AddSessionUseCase(ISessionRepository repository, ILicenseGuard guard, Session session)
        : base(guard)
    {
        _repository = repository;
        _session = session;
    }

    protected override Task<Result<int>> ExecuteCoreAsync(CancellationToken cancellationToken) =>
        _repository.AddAsync(_session, cancellationToken);
}

[WriteOperation]
public sealed class SetSessionStatusUseCase : WriteUseCase<bool>
{
    private readonly ISessionRepository _repository;
    private readonly int _sessionId;
    private readonly string _status;
    private readonly string? _cancelReason;

    public SetSessionStatusUseCase(
        ISessionRepository repository,
        ILicenseGuard guard,
        int sessionId,
        string status,
        string? cancelReason = null)
        : base(guard)
    {
        _repository = repository;
        _sessionId = sessionId;
        _status = status;
        _cancelReason = cancelReason;
    }

    protected override Task<Result<bool>> ExecuteCoreAsync(CancellationToken cancellationToken) =>
        _repository.SetStatusAsync(_sessionId, _status, _cancelReason, cancellationToken);
}

[WriteOperation]
public sealed class SaveAttendanceUseCase : WriteUseCase<int>
{
    private readonly IAttendanceRepository _repository;
    private readonly int _sessionId;
    private readonly int _studentId;
    private readonly string _status;
    private readonly string? _notes;

    public SaveAttendanceUseCase(
        IAttendanceRepository repository,
        ILicenseGuard guard,
        int sessionId,
        int studentId,
        string status,
        string? notes = null)
        : base(guard)
    {
        _repository = repository;
        _sessionId = sessionId;
        _studentId = studentId;
        _status = status;
        _notes = notes;
    }

    protected override Task<Result<int>> ExecuteCoreAsync(CancellationToken cancellationToken) =>
        _repository.UpsertAsync(_sessionId, _studentId, _status, _notes, cancellationToken);
}
