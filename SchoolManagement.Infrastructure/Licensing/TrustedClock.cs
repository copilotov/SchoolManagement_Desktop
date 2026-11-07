using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Time;
using SchoolManagement.Domain.Licensing;

namespace SchoolManagement.Infrastructure.Licensing;

/// <summary>
/// The application's only trusted "now". It merges the raw device clock with
/// the highest instant ever stored (DPAPI) and with the license issue anchor,
/// using <see cref="TrustedTimeEngine"/> (pure, tested separately).
///
/// The anchor is set by <c>LicenseRepository</c> only after the signature has
/// been verified, so an attacker cannot pin the clock with a forged payload.
/// </summary>
public sealed class TrustedClockService : ITrustedClock
{
    private readonly ITrustedTimeStorage _storage;
    private readonly ILocalClock _localClock;
    private readonly object _gate = new();

    private DateOnly? _anchorDate;

    public TrustedClockService(ITrustedTimeStorage storage, ILocalClock localClock)
    {
        _storage = storage;
        _localClock = localClock;
    }

    /// <summary>Called after a verified payload is loaded or activated.</summary>
    public void SetAnchor(DateOnly issuedAt)
    {
        lock (_gate)
        {
            _anchorDate = issuedAt;
        }
    }

    /// <summary>The most recent merged verdict, including the rollback flag.</summary>
    public TrustedTimeVerdict Current
    {
        get
        {
            lock (_gate)
            {
                return EvaluateLocked();
            }
        }
    }

    public DateTime UtcNow => Current.TrustedUtc;

    /// <summary>
    /// Local wall-clock components labelled UTC, matching how Drift stores
    /// audit timestamps. Always derived from the trusted instant, never from
    /// <c>DateTime.Now</c>.
    /// </summary>
    public DateTime LocalNow
    {
        get
        {
            lock (_gate)
            {
                var trustedUtc = DateTime.SpecifyKind(EvaluateLocked().TrustedUtc, DateTimeKind.Utc);
                var local = TimeZoneInfo.ConvertTimeFromUtc(trustedUtc, TimeZoneInfo.Local);
                return DateTime.SpecifyKind(local, DateTimeKind.Utc);
            }
        }
    }

    /// <summary>Evaluates and persists the new highest instant.</summary>
    public TrustedTimeVerdict EvaluateAndRecord()
    {
        lock (_gate)
        {
            var verdict = EvaluateLocked();
            _storage.Write(verdict.Updated);
            return verdict;
        }
    }

    private TrustedTimeVerdict EvaluateLocked()
    {
        var stored = _storage.Read();
        return TrustedTimeEngine.Evaluate(
            _localClock.UtcNow,
            _localClock.LocalNow,
            stored,
            _anchorDate);
    }
}
