using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Applies the SQLite pragmas that are connection-scoped. WAL mode is stored in
/// the database file itself (set once by <see cref="DatabaseInitializer"/>), but
/// the busy timeout and foreign-key enforcement must be repeated for every
/// connection EF opens.
/// </summary>
public sealed class SqlitePragmaInterceptor : DbConnectionInterceptor
{
    public const int BusyTimeoutMilliseconds = 5000;

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        Configure(connection);
    }

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Configure(connection);
        return Task.CompletedTask;
    }

    private static void Configure(DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            $"PRAGMA busy_timeout = {BusyTimeoutMilliseconds}; PRAGMA foreign_keys = ON;";
        command.ExecuteNonQuery();
    }
}
