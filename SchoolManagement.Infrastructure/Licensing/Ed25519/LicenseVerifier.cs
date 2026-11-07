using System.Text;
using NSec.Cryptography;
using SchoolManagement.Domain.Licensing;

namespace SchoolManagement.Infrastructure.Licensing.Ed25519;

public sealed record LicenseVerificationResult(
    bool IsValid,
    LicensePayload? Payload,
    LicenseFailureReason? FailureReason,
    string? PayloadJson = null)
{
    public static LicenseVerificationResult Valid(LicensePayload payload, string payloadJson) =>
        new(true, payload, null, payloadJson);

    public static LicenseVerificationResult Invalid(LicenseFailureReason reason) =>
        new(false, null, reason);
}

/// <summary>
/// Verifies <c>base64url(payload) + "." + base64url(signature)</c>.
///
/// Order matters and is deliberate:
/// <list type="number">
/// <item>decode the envelope (structure only);</item>
/// <item>verify the Ed25519 signature over the raw payload bytes;</item>
/// <item>only then parse dates/version/fingerprint.</item>
/// </list>
/// An attacker cannot learn the payload version or lure the parser with an
/// unsigned payload, and a tampered byte yields <c>BadSignature</c> rather than
/// a misleading <c>Malformed</c>.
/// </summary>
public sealed class LicenseVerifier
{
    private const int PublicKeyLength = 32;

    private readonly byte[] _publicKey;

    /// <summary>Uses the shipped <see cref="LicensePublicKey"/>.</summary>
    public LicenseVerifier()
        : this(LicensePublicKey.ToArray())
    {
    }

    /// <summary>Test/replaceable-key constructor (never an env-var override).</summary>
    public LicenseVerifier(byte[] publicKeyBytes)
    {
        _publicKey = publicKeyBytes is { Length: PublicKeyLength }
            ? (byte[])publicKeyBytes.Clone()
            : Array.Empty<byte>();
    }

    public LicenseVerifier(string publicKeyBase64)
        : this(TryDecodeBase64(publicKeyBase64))
    {
    }

    public bool IsConfigured => _publicKey.Length == PublicKeyLength;

    public LicenseVerificationResult Verify(string? licenseText, string currentFingerprintHash)
    {
        if (string.IsNullOrWhiteSpace(licenseText))
        {
            return LicenseVerificationResult.Invalid(LicenseFailureReason.Malformed);
        }

        if (!IsConfigured)
        {
            return LicenseVerificationResult.Invalid(LicenseFailureReason.NotConfigured);
        }

        if (!LicenseEnvelope.TryDecode(licenseText, out var payloadBytes, out var signatureBytes))
        {
            return LicenseVerificationResult.Invalid(LicenseFailureReason.Malformed);
        }

        // Signature first, semantics second.
        if (!VerifySignature(payloadBytes, signatureBytes))
        {
            return LicenseVerificationResult.Invalid(LicenseFailureReason.BadSignature);
        }

        if (!LicensePayload.TryParse(payloadBytes, out var payload, out var reason) || payload is null)
        {
            return LicenseVerificationResult.Invalid(reason);
        }

        if (!string.Equals(payload.Fingerprint, currentFingerprintHash?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return LicenseVerificationResult.Invalid(LicenseFailureReason.FingerprintMismatch);
        }

        return LicenseVerificationResult.Valid(payload, Encoding.UTF8.GetString(payloadBytes));
    }

    private bool VerifySignature(ReadOnlySpan<byte> payloadBytes, ReadOnlySpan<byte> signatureBytes)
    {
        try
        {
            var algorithm = SignatureAlgorithm.Ed25519;
            var publicKey = PublicKey.Import(algorithm, _publicKey, KeyBlobFormat.RawPublicKey);
            return algorithm.Verify(publicKey, payloadBytes, signatureBytes);
        }
        catch (Exception)
        {
            // Wrong key length, malformed signature length, provider failure:
            // all mean "this license did not come from us".
            return false;
        }
    }

    private static byte[] TryDecodeBase64(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<byte>();
        }

        try
        {
            return Convert.FromBase64String(value.Trim());
        }
        catch (FormatException)
        {
            return Array.Empty<byte>();
        }
    }
}
