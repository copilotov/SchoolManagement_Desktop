namespace SchoolManagement.Desktop.Services;

/// <summary>
/// Phase D2 - persists the main window geometry. It is deliberately separate
/// from <c>settings.json</c>, which the documentation reserves for language and
/// theme so both can be read before the database opens.
/// </summary>
public interface IWindowStateStore
{
    /// <summary>Returns <see cref="WindowPlacement.Default"/> when no file exists or it is unreadable.</summary>
    Task<WindowPlacement> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns false instead of throwing when the disk is unavailable.</summary>
    Task<bool> SaveAsync(WindowPlacement placement, CancellationToken cancellationToken = default);
}
