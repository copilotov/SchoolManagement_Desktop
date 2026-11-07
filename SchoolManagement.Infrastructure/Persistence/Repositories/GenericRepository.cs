using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Time;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>
/// Only get-by-id, add, update and delete: feature repositories expose the
/// filtered projections their screens need.
/// </summary>
public class GenericRepository<TEntity> : RepositoryBase, IGenericRepository<TEntity>
    where TEntity : class, IEntity
{
    public GenericRepository(
        IDbContextFactory<SchoolManagementDbContext> contextFactory,
        ITrustedClock clock,
        ILogger<GenericRepository<TEntity>> logger)
        : base(contextFactory, clock, logger)
    {
    }

    public Task<Result<TEntity?>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => RunAsync<TEntity?>(
            $"{typeof(TEntity).Name}.GetById",
            async context => await context.Set<TEntity>()
                .FindAsync([id], cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    public Task<Result<int>> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return RunAsync(
            $"{typeof(TEntity).Name}.Add",
            async context =>
            {
                RepositoryHelper.ApplyCreatedTimestamps(entity, Clock.LocalNow);
                context.Set<TEntity>().Add(entity);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return entity.Id;
            },
            cancellationToken,
            TranslationKeys.Errors.SaveError);
    }

    public Task<Result<bool>> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return RunResultAsync(
            $"{typeof(TEntity).Name}.Update",
            async context =>
            {
                RepositoryHelper.ApplyUpdatedTimestamp(entity, Clock.LocalNow);
                context.Entry(entity).State = EntityState.Modified;
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return Result<bool>.Success(true);
            },
            cancellationToken,
            TranslationKeys.Errors.SaveError);
    }

    public virtual Task<Result<bool>> DeleteAsync(int id, CancellationToken cancellationToken = default)
        => RunResultAsync(
            $"{typeof(TEntity).Name}.Delete",
            async context =>
            {
                var entity = await context.Set<TEntity>()
                    .FindAsync([id], cancellationToken)
                    .ConfigureAwait(false);

                if (entity is null)
                {
                    return Result<bool>.Fail(Failure.NotFound(TranslationKeys.Errors.NotFound));
                }

                context.Set<TEntity>().Remove(entity);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return Result<bool>.Success(true);
            },
            cancellationToken,
            TranslationKeys.Errors.SaveError);
}
