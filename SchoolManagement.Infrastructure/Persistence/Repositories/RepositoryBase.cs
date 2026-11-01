using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Time;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>
/// Shared exception-to-<see cref="Failure"/> handling. Repositories never let a
/// database exception reach the UI, but cancellation is always rethrown.
/// </summary>
internal static class RepositoryHelper
{
    public static async Task<Result<T>> RunAsync<T>(
        IDbContextFactory<SchoolManagementDbContext> contextFactory,
        ILogger logger,
        string operation,
        Func<SchoolManagementDbContext, Task<T>> action,
        CancellationToken cancellationToken,
        string failureKey = TranslationKeys.Errors.DatabaseFailure)
    {
        try
        {
            await using var context = await contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);
            var value = await action(context).ConfigureAwait(false);
            return Result<T>.Success(value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Repository operation {Operation} failed.", operation);
            return Result<T>.Fail(Failure.Database(failureKey, exception.Message));
        }
    }

    public static async Task<Result<T>> RunResultAsync<T>(
        IDbContextFactory<SchoolManagementDbContext> contextFactory,
        ILogger logger,
        string operation,
        Func<SchoolManagementDbContext, Task<Result<T>>> action,
        CancellationToken cancellationToken,
        string failureKey = TranslationKeys.Errors.DatabaseFailure)
    {
        try
        {
            await using var context = await contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);
            return await action(context).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Repository operation {Operation} failed.", operation);
            return Result<T>.Fail(Failure.Database(failureKey, exception.Message));
        }
    }

    public static void ApplyCreatedTimestamps(IEntity entity, DateTime now)
    {
        if (entity is IAuditableEntity auditable)
        {
            if (auditable.CreatedAt == default)
            {
                auditable.CreatedAt = now;
            }

            auditable.UpdatedAt = now;
            return;
        }

        if (entity is ICreatedEntity created && created.CreatedAt == default)
        {
            created.CreatedAt = now;
        }

        if (entity is IUpdatedEntity updated)
        {
            updated.UpdatedAt = now;
        }
    }

    public static void ApplyUpdatedTimestamp(IEntity entity, DateTime now)
    {
        if (entity is IAuditableEntity auditable)
        {
            auditable.UpdatedAt = now;
        }
        else if (entity is IUpdatedEntity updated)
        {
            updated.UpdatedAt = now;
        }
    }
}

/// <summary>Common dependencies and helpers for every repository.</summary>
public abstract class RepositoryBase
{
    protected RepositoryBase(
        IDbContextFactory<SchoolManagementDbContext> contextFactory,
        ITrustedClock clock,
        ILogger logger)
    {
        ContextFactory = contextFactory;
        Clock = clock;
        Logger = logger;
    }

    protected IDbContextFactory<SchoolManagementDbContext> ContextFactory { get; }

    protected ITrustedClock Clock { get; }

    protected ILogger Logger { get; }

    protected Task<Result<T>> RunAsync<T>(
        string operation,
        Func<SchoolManagementDbContext, Task<T>> action,
        CancellationToken cancellationToken,
        string failureKey = TranslationKeys.Errors.DatabaseFailure)
        => RepositoryHelper.RunAsync(ContextFactory, Logger, operation, action, cancellationToken, failureKey);

    protected Task<Result<T>> RunResultAsync<T>(
        string operation,
        Func<SchoolManagementDbContext, Task<Result<T>>> action,
        CancellationToken cancellationToken,
        string failureKey = TranslationKeys.Errors.DatabaseFailure)
        => RepositoryHelper.RunResultAsync(ContextFactory, Logger, operation, action, cancellationToken, failureKey);
}
