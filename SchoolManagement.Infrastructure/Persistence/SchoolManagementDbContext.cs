using Microsoft.EntityFrameworkCore;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the local SQLite file. Entity sets are added in the
/// data-model phase; Phase 1 only opens/creates the file and verifies it.
/// The context is never exposed to ViewModels (use cases/repositories only).
/// </summary>
public sealed class SchoolManagementDbContext : DbContext
{
    public SchoolManagementDbContext(DbContextOptions<SchoolManagementDbContext> options)
        : base(options)
    {
    }
}
