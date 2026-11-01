using SchoolManagement.Application.Common;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Repositories;

/// <summary>
/// Minimal generic repository. It intentionally has no "list everything"
/// method: feature-specific repositories expose projections with paging or a
/// deliberate filter so a screen can never load a whole table by accident.
/// </summary>
public interface IGenericRepository<TEntity>
    where TEntity : class, IEntity
{
    Task<Result<TEntity?>> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<Result<int>> AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    Task<Result<bool>> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    Task<Result<bool>> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
