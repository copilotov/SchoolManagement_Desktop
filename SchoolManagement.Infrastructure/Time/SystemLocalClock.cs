using SchoolManagement.Application.Time;

namespace SchoolManagement.Infrastructure.Time;

/// <summary>
/// The raw device clock. It is read only here and only to feed the trusted
/// clock merge; no license decision uses it directly. Keeping this single
/// wrapper is what the "no DateTime.Now/UtcNow in license validity" rule means
/// in practice.
/// </summary>
public sealed class SystemLocalClock : ILocalClock
{
    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime LocalNow => DateTime.Now;
}
