using SchoolManagement.Application.Time;

namespace SchoolManagement.Infrastructure.Time;

/// <summary>
/// The single implementation of "now". <see cref="UtcNow"/> is a real UTC
/// instant used for license validity. <see cref="LocalNow"/> keeps the local
/// wall-clock components but labels them UTC, matching how Drift stores
/// timestamps in SQLite.
/// </summary>
public sealed class SystemTrustedClock : ITrustedClock
{
    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime LocalNow => DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc);
}
