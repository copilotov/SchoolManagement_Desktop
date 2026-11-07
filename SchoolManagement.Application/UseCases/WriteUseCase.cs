using SchoolManagement.Application.Common;
using SchoolManagement.Application.Licensing;

namespace SchoolManagement.Application.UseCases;

/// <summary>
/// Template for every write use case. <see cref="ExecuteAsync"/> is the only
/// public entry point and it always runs the license guard before the concrete
/// operation. A subclass cannot skip it because the operation itself lives in
/// <see cref="ExecuteCoreAsync"/>.
/// </summary>
public abstract class WriteUseCaseBase
{
    protected WriteUseCaseBase(ILicenseGuard guard)
    {
        Guard = guard ?? throw new ArgumentNullException(nameof(guard));
    }

    protected ILicenseGuard Guard { get; }
}

public abstract class WriteUseCase<T> : WriteUseCaseBase
{
    protected WriteUseCase(ILicenseGuard guard)
        : base(guard)
    {
    }

    public async Task<Result<T>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var gate = Guard.EnsureCanWrite();
        if (gate.IsFailure)
        {
            return Result<T>.Fail(gate.Failure!);
        }

        return await ExecuteCoreAsync(cancellationToken).ConfigureAwait(false);
    }

    protected abstract Task<Result<T>> ExecuteCoreAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Same guard-first template for use cases whose input is a request object
/// instead of constructor state. The UI can therefore inject the use case from
/// DI and still cannot reach the repository without passing the license gate.
/// </summary>
public abstract class WriteUseCase<TRequest, TResult> : WriteUseCaseBase
{
    protected WriteUseCase(ILicenseGuard guard)
        : base(guard)
    {
    }

    public async Task<Result<TResult>> ExecuteAsync(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        var gate = Guard.EnsureCanWrite();
        if (gate.IsFailure)
        {
            return Result<TResult>.Fail(gate.Failure!);
        }

        return await ExecuteCoreAsync(request, cancellationToken).ConfigureAwait(false);
    }

    protected abstract Task<Result<TResult>> ExecuteCoreAsync(
        TRequest request,
        CancellationToken cancellationToken);
}
