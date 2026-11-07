using System.Text;

namespace SchoolManagement.Domain.Licensing;

/// <summary>
/// base64url encoding compatible with Dart's <c>base64UrlEncode</c>: URL-safe
/// alphabet and <c>=</c> padding on output. Decoding accepts padded and
/// unpadded input, because licenses are copied by hand.
/// </summary>
public static class Base64Url
{
    public static string Encode(ReadOnlySpan<byte> data)
    {
        var base64 = Convert.ToBase64String(data);
        return base64
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static bool TryDecode(string? value, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!(c is >= 'A' and <= 'Z'
                or >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '-'
                or '_'
                or '='))
            {
                return false;
            }
        }

        var normalized = value.Replace('-', '+').Replace('_', '/');
        var padding = normalized.Length % 4;
        if (padding == 2)
        {
            normalized += "==";
        }
        else if (padding == 3)
        {
            normalized += "=";
        }
        else if (padding == 1)
        {
            return false;
        }

        try
        {
            bytes = Convert.FromBase64String(normalized);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

/// <summary>
/// <c>base64url(payload) + "." + base64url(signature)</c>, the exact envelope
/// produced by the phone's <c>tool/license_tool.dart</c>.
/// </summary>
public static class LicenseEnvelope
{
    public static string Encode(string payloadJson, ReadOnlySpan<byte> signature)
    {
        ArgumentNullException.ThrowIfNull(payloadJson);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
        return Base64Url.Encode(payloadBytes) + "." + Base64Url.Encode(signature);
    }

    public static bool TryDecode(string? licenseText, out byte[] payloadBytes, out byte[] signatureBytes)
    {
        payloadBytes = Array.Empty<byte>();
        signatureBytes = Array.Empty<byte>();

        if (string.IsNullOrWhiteSpace(licenseText))
        {
            return false;
        }

        var trimmed = licenseText.Trim();
        var separator = trimmed.IndexOf('.', StringComparison.Ordinal);
        if (separator <= 0 || separator != trimmed.LastIndexOf('.'))
        {
            return false;
        }

        var payloadPart = trimmed[..separator];
        var signaturePart = trimmed[(separator + 1)..];

        return Base64Url.TryDecode(payloadPart, out payloadBytes) &&
               Base64Url.TryDecode(signaturePart, out signatureBytes);
    }
}
