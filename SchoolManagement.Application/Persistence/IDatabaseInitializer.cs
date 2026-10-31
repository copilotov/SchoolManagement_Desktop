namespace SchoolManagement.Application.Persistence;

/// <summary>
/// Opens (or creates) the local SQLite database. It must not run migrations
/// against an existing phone database; schema changes are additive and handled
/// in a later phase.
/// </summary>
public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
