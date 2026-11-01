namespace SchoolManagement.Application.Time;

/// <summary>
/// The only source of "now" allowed in the application. License validity must
/// use <see cref="UtcNow"/> and never <c>DateTime.Now</c>/<c>DateTime.UtcNow</c>
/// directly. Audit columns use <see cref="LocalNow"/> because the phone stores
/// local wall-clock timestamps in SQLite.
/// </summary>
public interface ITrustedClock
{
    DateTime UtcNow { get; }

    DateTime LocalNow { get; }
}
