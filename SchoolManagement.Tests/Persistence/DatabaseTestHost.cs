using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Paths;
using SchoolManagement.Application.Time;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Tests.Persistence;

/// <summary>
/// Creates a throw-away SQLite file, runs the real <see cref="DatabaseInitializer"/>
/// and exposes a factory for repositories. Every test gets its own directory.
/// </summary>
internal sealed class DatabaseTestHost : IAsyncDisposable
{
    private readonly string _directory;

    private DatabaseTestHost(string directory)
    {
        _directory = directory;
        Paths = new AppPaths(directory, directory);
        Clock = new FixedClock(new DateTime(2026, 10, 31, 9, 0, 0, DateTimeKind.Utc));
    }

    public AppPaths Paths { get; }

    public FixedClock Clock { get; }

    public string ConnectionString => new SqliteConnectionStringBuilder
    {
        DataSource = Paths.DatabaseFile,
        ForeignKeys = true,
        Cache = SqliteCacheMode.Shared,
        DefaultTimeout = 5,
    }.ToString();

    public static DatabaseTestHost Create()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            string.Create(CultureInfo.InvariantCulture, $"school-db-{Guid.NewGuid():N}"));

        Directory.CreateDirectory(directory);
        return new DatabaseTestHost(directory);
    }

    public async Task InitializeAsync()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.Tr(TranslationKeys.App.Name).Returns("Test School");

        var initializer = new DatabaseInitializer(
            Paths,
            localization,
            Clock,
            NullLogger<DatabaseInitializer>.Instance);

        await initializer.InitializeAsync();
    }

    public IDbContextFactory<SchoolManagementDbContext> CreateFactory()
    {
        var options = new DbContextOptionsBuilder<SchoolManagementDbContext>()
            .UseSqlite(ConnectionString)
            .AddInterceptors(new SqlitePragmaInterceptor())
            .Options;

        return new TestDbContextFactory(options);
    }

    public async Task<SqliteConnection> OpenAsync()
    {
        var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task ExecuteAsync(string sql)
    {
        await using var connection = await OpenAsync();
        await ExecuteAsync(connection, sql);
    }

    public static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    public static async Task<object?> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    public static async Task<long> LongAsync(SqliteConnection connection, string sql)
        => Convert.ToInt64(await ScalarAsync(connection, sql), CultureInfo.InvariantCulture);

    public static async Task<string?> TextAsync(SqliteConnection connection, string sql)
        => Convert.ToString(await ScalarAsync(connection, sql), CultureInfo.InvariantCulture);

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();

        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A lingering pooled handle must never fail a test run.
        }

        return ValueTask.CompletedTask;
    }

    private sealed class TestDbContextFactory : IDbContextFactory<SchoolManagementDbContext>
    {
        private readonly DbContextOptions<SchoolManagementDbContext> _options;

        public TestDbContextFactory(DbContextOptions<SchoolManagementDbContext> options)
        {
            _options = options;
        }

        public SchoolManagementDbContext CreateDbContext() => new(_options);

        public Task<SchoolManagementDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}

internal sealed class FixedClock : ITrustedClock
{
    public FixedClock(DateTime value)
    {
        UtcNow = value;
        LocalNow = DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    public DateTime UtcNow { get; }

    public DateTime LocalNow { get; }
}
