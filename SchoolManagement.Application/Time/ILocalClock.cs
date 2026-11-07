namespace SchoolManagement.Application.Time;

/// <summary>
/// Access to the raw device clock. This is the only place where
/// <c>DateTime.UtcNow</c>/<c>DateTime.Now</c> is read; license validity itself
/// uses <see cref="ITrustedClock"/>, which merges this raw value with the
/// highest date ever observed.
/// </summary>
public interface ILocalClock
{
    DateTime UtcNow { get; }

    DateTime LocalNow { get; }
}
