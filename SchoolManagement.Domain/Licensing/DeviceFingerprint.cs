namespace SchoolManagement.Domain.Licensing;

/// <summary>
/// SHA-256 fingerprint of the machine, shown to the user as 64 hex characters
/// plus a short 4x4 code for reading over the phone.
/// </summary>
public sealed record DeviceFingerprint(
    string Hash,
    bool IsStrong,
    bool UsedFallback)
{
    public bool IsValid =>
        Hash.Length == 64 && Hash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    public string ShortCode
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Hash) || Hash.Length < 16)
            {
                return Hash ?? string.Empty;
            }

            var s = Hash.ToUpperInvariant();
            return $"{s[..4]}-{s[4..8]}-{s[8..12]}-{s[12..16]}";
        }
    }
}
