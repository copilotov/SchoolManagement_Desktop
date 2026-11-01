using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Paths;
using SchoolManagement.Application.Persistence;
using SchoolManagement.Application.Time;
using SchoolManagement.Infrastructure.Persistence.Converters;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Opens the phone database, creating the original Drift schema only when the
/// file is new. It never runs EF migrations: schema versioning follows the
/// phone's <c>PRAGMA user_version</c> (1 = before <c>enrollments.group_id</c>,
/// 2 = current).
/// </summary>
public sealed class DatabaseInitializer : IDatabaseInitializer
{
    private const int BusyTimeoutMilliseconds = 5000;

    private readonly AppPaths _paths;
    private readonly ILocalizationService _localization;
    private readonly ITrustedClock _clock;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        AppPaths paths,
        ILocalizationService localization,
        ITrustedClock clock,
        ILogger<DatabaseInitializer> logger)
    {
        _paths = paths;
        _localization = localization;
        _clock = clock;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_paths.DataDirectory);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _paths.DatabaseFile,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = 5,
        }.ToString();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await ExecuteAsync(connection, $"PRAGMA busy_timeout = {BusyTimeoutMilliseconds};", cancellationToken)
            .ConfigureAwait(false);
        await ExecuteAsync(connection, "PRAGMA journal_mode = WAL;", cancellationToken).ConfigureAwait(false);

        var created = await EnsureCurrentSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        // Enabled after DDL so an old file with orphan rows is not rejected mid-upgrade.
        await ExecuteAsync(connection, "PRAGMA foreign_keys = ON;", cancellationToken).ConfigureAwait(false);

        if (created)
        {
            await SeedAsync(connection, cancellationToken).ConfigureAwait(false);
        }

        var integrity = await ScalarAsync(connection, "PRAGMA integrity_check;", cancellationToken)
            .ConfigureAwait(false);
        var status = integrity?.ToString();

        if (!string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"SQLite integrity check failed: {status ?? "<null>"}");
        }

        _logger.LogInformation("SQLite database ready at {DatabasePath}.", _paths.DatabaseFile);
    }

    /// <summary>Returns true when the file was created from scratch.</summary>
    private async Task<bool> EnsureCurrentSchemaAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var version = Convert.ToInt32(
            await ScalarAsync(connection, "PRAGMA user_version;", cancellationToken).ConfigureAwait(false),
            System.Globalization.CultureInfo.InvariantCulture);

        if (version > OriginalSchema.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"The database schema version is {version}, but this build supports at most " +
                $"{OriginalSchema.CurrentVersion}. Install a newer desktop build; the file was not modified.");
        }

        var tableCount = Convert.ToInt32(
            await ScalarAsync(
                connection,
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';",
                cancellationToken).ConfigureAwait(false),
            System.Globalization.CultureInfo.InvariantCulture);

        var hasTables = tableCount > 0;
        var created = false;

        if (version == 0 && hasTables)
        {
            // A Drift file that never wrote user_version: infer the version from
            // the only column added by the v2 migration.
            version = await HasColumnAsync(connection, "enrollments", "group_id", cancellationToken)
                .ConfigureAwait(false)
                ? OriginalSchema.CurrentVersion
                : 1;
        }

        if (!hasTables)
        {
            await ExecuteAsync(connection, OriginalSchema.CreateV2Script, cancellationToken).ConfigureAwait(false);
            await SetUserVersionAsync(connection, OriginalSchema.CurrentVersion, cancellationToken)
                .ConfigureAwait(false);
            created = true;
            _logger.LogInformation("Created the original schema v2 in {DatabasePath}.", _paths.DatabaseFile);
        }
        else if (version == 1)
        {
            await UpgradeV1ToV2Async(connection, cancellationToken).ConfigureAwait(false);
            _logger.LogWarning(
                "Upgraded a v1 database at {DatabasePath}: enrollments.group_id was added with default 0, " +
                "so old rows must be enrolled into a group again.",
                _paths.DatabaseFile);
        }
        else if (version == OriginalSchema.CurrentVersion)
        {
            var hasGroupId = await HasColumnAsync(connection, "enrollments", "group_id", cancellationToken)
                .ConfigureAwait(false);
            if (!hasGroupId)
            {
                throw new InvalidOperationException(
                    "The database reports schema version 2 but enrollments.group_id is missing. " +
                    "The file is inconsistent and was not modified.");
            }
        }

        await ValidateRequiredTablesAsync(connection, cancellationToken).ConfigureAwait(false);
        return created;
    }

    private static async Task UpgradeV1ToV2Async(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var hasGroupId = await HasColumnAsync(connection, "enrollments", "group_id", cancellationToken)
            .ConfigureAwait(false);

        if (!hasGroupId)
        {
            await ExecuteAsync(connection, OriginalSchema.UpgradeV1ToV2Script, cancellationToken)
                .ConfigureAwait(false);
        }

        await SetUserVersionAsync(connection, OriginalSchema.CurrentVersion, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task ValidateRequiredTablesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                existing.Add(reader.GetString(0));
            }
        }

        var missing = OriginalSchema.RequiredTables.Where(name => !existing.Contains(name)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"The database is missing {missing.Length} required table(s): {string.Join(", ", missing)}. " +
                "The file is not an original school-management database and was not modified.");
        }
    }

    private async Task SeedAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var schoolName = _localization.Tr(TranslationKeys.App.Name);
        var now = UnixSecondsDateTimeConverter.ToUnixSeconds(_clock.LocalNow);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = """
                INSERT INTO "schools" ("id", "name", "default_language", "timezone", "is_active", "created_at", "updated_at")
                VALUES (1, $name, 'ar', 'Africa/Algiers', 1, $now, $now);
                """;
            command.Parameters.AddWithValue("$name", schoolName);
            command.Parameters.AddWithValue("$now", now);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText =
                "INSERT INTO \"fee_settings\" (\"id\", \"updated_at\") VALUES (1, $now);";
            command.Parameters.AddWithValue("$now", now);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = """
                INSERT INTO "system_settings" ("id", "key", "value", "updated_at")
                VALUES (1, 'absence_limit', '5', $now);
                """;
            command.Parameters.AddWithValue("$now", now);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Seeded the school row, fee settings and the default absence limit.");
    }

    private static async Task<bool> HasColumnAsync(
        SqliteConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        // Table and column names are internal constants, never user input.
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{table}\");";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static Task SetUserVersionAsync(
        SqliteConnection connection,
        int version,
        CancellationToken cancellationToken)
        => ExecuteAsync(connection, $"PRAGMA user_version = {version};", cancellationToken);

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<object?> ScalarAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }
}
