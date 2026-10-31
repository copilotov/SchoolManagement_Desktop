using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Paths;
using SchoolManagement.Application.Persistence;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Opens the existing phone database without migrating it. When no file exists
/// a new SQLite file is created (tables arrive with the data-model phase).
/// </summary>
public sealed class DatabaseInitializer : IDatabaseInitializer
{
    private readonly IDbContextFactory<SchoolManagementDbContext> _contextFactory;
    private readonly AppPaths _paths;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        IDbContextFactory<SchoolManagementDbContext> contextFactory,
        AppPaths paths,
        ILogger<DatabaseInitializer> logger)
    {
        _contextFactory = contextFactory;
        _paths = paths;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_paths.DataDirectory);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // EnsureCreated is safe for an existing Drift database: it returns false
        // as soon as it sees existing tables and does not alter them.
        await context.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        await context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var status = result?.ToString();

            if (!string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"SQLite integrity check failed: {status ?? "<null>"}");
            }

            _logger.LogInformation("SQLite database ready at {DatabasePath}.", _paths.DatabaseFile);
        }
        finally
        {
            await context.Database.CloseConnectionAsync().ConfigureAwait(false);
        }
    }
}
