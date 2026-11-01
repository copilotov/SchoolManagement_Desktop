using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Persistence.Converters;

namespace SchoolManagement.Tests.Persistence;

public sealed class DatabaseInitializerTests
{
    [Fact]
    public async Task New_database_creates_the_28_original_tables_and_seeds_once()
    {
        await using var host = DatabaseTestHost.Create();

        await host.InitializeAsync();

        await using var connection = await host.OpenAsync();

        (await DatabaseTestHost.LongAsync(connection, "PRAGMA user_version;")).Should().Be(2);
        (await DatabaseTestHost.TextAsync(connection, "PRAGMA journal_mode;")).Should().Be("wal");

        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        tables.Should().Contain(OriginalSchema.RequiredTables);
        OriginalSchema.RequiredTables.Should().HaveCount(28);

        (await DatabaseTestHost.LongAsync(connection, "SELECT COUNT(*) FROM schools;")).Should().Be(1);
        (await DatabaseTestHost.TextAsync(connection, "SELECT name FROM schools WHERE id = 1;"))
            .Should().Be("Test School");
        (await DatabaseTestHost.LongAsync(connection, "SELECT COUNT(*) FROM fee_settings;")).Should().Be(1);
        (await DatabaseTestHost.TextAsync(
                connection,
                "SELECT value FROM system_settings WHERE key = 'absence_limit';"))
            .Should().Be("5");
    }

    [Fact]
    public async Task Initialize_is_idempotent_and_never_seeds_twice()
    {
        await using var host = DatabaseTestHost.Create();

        await host.InitializeAsync();
        await host.InitializeAsync();

        await using var connection = await host.OpenAsync();

        (await DatabaseTestHost.LongAsync(connection, "SELECT COUNT(*) FROM schools;")).Should().Be(1);
        (await DatabaseTestHost.LongAsync(connection, "SELECT COUNT(*) FROM fee_settings;")).Should().Be(1);
        (await DatabaseTestHost.LongAsync(connection, "SELECT COUNT(*) FROM system_settings;")).Should().Be(1);
    }

    [Fact]
    public async Task A_newer_schema_version_is_refused_without_touching_the_file()
    {
        await using var host = DatabaseTestHost.Create();
        await host.ExecuteAsync("PRAGMA user_version = 3;");

        var act = () => host.InitializeAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*version is 3*");

        await using var connection = await host.OpenAsync();
        (await DatabaseTestHost.LongAsync(connection, "PRAGMA user_version;")).Should().Be(3);
        (await DatabaseTestHost.LongAsync(
                connection,
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table';"))
            .Should().Be(0);
    }

    [Fact]
    public async Task A_version_1_file_is_upgraded_with_an_empty_group_id()
    {
        await using var host = DatabaseTestHost.Create();

        await host.ExecuteAsync(OriginalSchema.CreateV2Script);
        await host.ExecuteAsync("""
            DROP TABLE "enrollments";
            CREATE TABLE "enrollments" (
              "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
              "school_id" INTEGER NOT NULL DEFAULT 1,
              "student_id" INTEGER NOT NULL,
              "enrollment_date" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP)),
              "registration_fee" REAL NOT NULL DEFAULT 0.0,
              "status" TEXT NOT NULL DEFAULT 'active',
              "notes" TEXT NULL,
              "created_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP)),
              "updated_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP))
            );
            PRAGMA user_version = 1;
            INSERT INTO "enrollments" ("id", "student_id") VALUES (1, 7);
            """);

        await host.InitializeAsync();

        await using var connection = await host.OpenAsync();

        (await DatabaseTestHost.LongAsync(connection, "PRAGMA user_version;")).Should().Be(2);
        (await DatabaseTestHost.LongAsync(
                connection,
                "SELECT group_id FROM enrollments WHERE id = 1;"))
            .Should().Be(0);

        // No seed: the file already existed.
        (await DatabaseTestHost.LongAsync(connection, "SELECT COUNT(*) FROM schools;")).Should().Be(0);
    }

    [Fact]
    public async Task Unique_student_code_and_system_setting_key_are_enforced()
    {
        await using var host = DatabaseTestHost.Create();
        await host.InitializeAsync();

        await host.ExecuteAsync(
            "INSERT INTO students (student_code, first_name, last_name) VALUES ('S001', 'A', 'B');");

        var duplicateCode = () => host.ExecuteAsync(
            "INSERT INTO students (student_code, first_name, last_name) VALUES ('S001', 'C', 'D');");

        await duplicateCode.Should().ThrowAsync<SqliteException>();

        var duplicateSetting = () => host.ExecuteAsync(
            "INSERT INTO system_settings (key, value) VALUES ('absence_limit', '9');");

        await duplicateSetting.Should().ThrowAsync<SqliteException>();
    }

    [Fact]
    public async Task An_existing_original_v2_file_is_read_with_unix_second_dates()
    {
        await using var host = DatabaseTestHost.Create();

        await host.ExecuteAsync($"""
            {OriginalSchema.CreateV2Script}
            PRAGMA user_version = 2;
            INSERT INTO "students"
              ("id", "student_code", "first_name", "last_name", "date_of_birth", "created_at", "updated_at")
            VALUES (1, 'OLD-1', 'Old', 'Student', 1700000000, 1700000000, 1700000000);
            """);

        await host.InitializeAsync();

        var factory = host.CreateFactory();

        await using (var context = await factory.CreateDbContextAsync())
        {
            var student = await context.Students.SingleAsync(x => x.Id == 1);

            student.DateOfBirth.Should().Be(UnixSecondsDateTimeConverter.FromUnixSeconds(1700000000));
            student.DateOfBirth!.Value.Kind.Should().Be(DateTimeKind.Utc);
            student.CreatedAt.Should().Be(UnixSecondsDateTimeConverter.FromUnixSeconds(1700000000));

            student.DateOfBirth = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);
            student.UpdatedAt = host.Clock.LocalNow;
            await context.SaveChangesAsync();
        }

        await using var connection = await host.OpenAsync();
        var storedSeconds = await DatabaseTestHost.LongAsync(
            connection,
            "SELECT date_of_birth FROM students WHERE id = 1;");

        storedSeconds.Should().Be(
            UnixSecondsDateTimeConverter.ToUnixSeconds(
                new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc)));
    }
}
