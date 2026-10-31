namespace SchoolManagement.Application.Settings;

/// <summary>
/// Small JSON store for theme/language. It is deliberately separate from the
/// SQLite database: preferences must be readable before the database opens.
/// </summary>
public interface IAppSettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns false instead of throwing when the disk is unavailable.</summary>
    Task<bool> SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
