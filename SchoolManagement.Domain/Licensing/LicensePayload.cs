using System.Text;
using System.Text.Json;

namespace SchoolManagement.Domain.Licensing;

/// <summary>
/// The signed payload of a license. The canonical JSON form is part of the
/// signature: key order, compact separators and raw UTF-8 output replicate
/// Dart's <c>jsonEncode</c>, because the phone's <c>tool/license_tool.dart</c>
/// signs exactly those bytes. Never re-order or re-format this writer without
/// a compatibility vector.
/// </summary>
public sealed record LicensePayload(
    int Version,
    string Fingerprint,
    DateOnly ExpiryDate,
    string? LicensedTo,
    DateOnly IssuedAt,
    DateOnly? NotBefore,
    string KeyId)
{
    public const int MinimumVersion = 1;
    public const int CurrentVersion = 2;
    public const string DefaultKeyId = "k1";

    /// <summary><c>nbf</c> after <c>exp</c> is a malformed license.</summary>
    public bool IsConsistent => !(NotBefore is { } nbf && nbf > ExpiryDate);

    /// <summary>
    /// Writes the canonical payload bytes: <c>{"v":..,"fp":"..","exp":"..","to":..,"iat":".."[,"nbf":".."],"kid":".."}</c>
    /// with no whitespace and no escaping of non-ASCII characters.
    /// </summary>
    public string WriteCanonicalJson()
    {
        var builder = new StringBuilder(160);
        builder.Append("{\"v\":").Append(Version.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.Append(",\"fp\":");
        WriteJsonString(builder, Fingerprint);
        builder.Append(",\"exp\":\"").Append(LicenseDate.ToIso(ExpiryDate)).Append('"');
        builder.Append(",\"to\":");
        if (LicensedTo is null)
        {
            builder.Append("null");
        }
        else
        {
            WriteJsonString(builder, LicensedTo);
        }

        builder.Append(",\"iat\":\"").Append(LicenseDate.ToIso(IssuedAt)).Append('"');

        if (NotBefore is { } notBefore)
        {
            builder.Append(",\"nbf\":\"").Append(LicenseDate.ToIso(notBefore)).Append('"');
        }

        builder.Append(",\"kid\":");
        WriteJsonString(builder, KeyId);
        builder.Append('}');
        return builder.ToString();
    }

    /// <summary>
    /// Parses a verified payload. The caller must validate the Ed25519
    /// signature over the raw bytes first; this method only applies semantics.
    /// </summary>
    public static bool TryParse(
        ReadOnlySpan<byte> utf8Json,
        out LicensePayload? payload,
        out LicenseFailureReason reason)
    {
        payload = null;
        reason = LicenseFailureReason.Malformed;

        if (utf8Json.IsEmpty)
        {
            return false;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(utf8Json.ToArray(), new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            });
        }
        catch (JsonException)
        {
            return false;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
            {
                // A duplicate key would be ambiguous and a signature-smuggling vector.
                if (!fields.TryAdd(property.Name, property.Value))
                {
                    return false;
                }
            }

            if (!fields.TryGetValue("v", out var versionElement) ||
                versionElement.ValueKind != JsonValueKind.Number ||
                !versionElement.TryGetInt32(out var version))
            {
                return false;
            }

            if (version < MinimumVersion)
            {
                return false;
            }

            if (version > CurrentVersion)
            {
                reason = LicenseFailureReason.UnsupportedVersion;
                return false;
            }

            if (!TryReadString(fields, "fp", required: true, out var fingerprint) ||
                string.IsNullOrWhiteSpace(fingerprint))
            {
                return false;
            }

            if (!fields.TryGetValue("exp", out var expElement) ||
                expElement.ValueKind != JsonValueKind.String ||
                !LicenseDate.TryParse(expElement.GetString(), out var expiryDate))
            {
                return false;
            }

            // "to" is always written by the issuer, as null or a string.
            if (!fields.TryGetValue("to", out var toElement))
            {
                return false;
            }

            string? licensedTo;
            switch (toElement.ValueKind)
            {
                case JsonValueKind.Null:
                    licensedTo = null;
                    break;
                case JsonValueKind.String:
                    licensedTo = toElement.GetString();
                    if (string.IsNullOrEmpty(licensedTo))
                    {
                        licensedTo = null;
                    }

                    break;
                default:
                    return false;
            }

            if (!fields.TryGetValue("iat", out var iatElement) ||
                iatElement.ValueKind != JsonValueKind.String ||
                !LicenseDate.TryParse(iatElement.GetString(), out var issuedAt))
            {
                return false;
            }

            DateOnly? notBefore = null;
            if (fields.TryGetValue("nbf", out var nbfElement) && nbfElement.ValueKind != JsonValueKind.Null)
            {
                if (nbfElement.ValueKind != JsonValueKind.String ||
                    !LicenseDate.TryParse(nbfElement.GetString(), out var nbf))
                {
                    return false;
                }

                notBefore = nbf;
            }

            var keyId = DefaultKeyId;
            if (TryReadString(fields, "kid", required: false, out var kid) && !string.IsNullOrWhiteSpace(kid))
            {
                // An unknown kid is deliberately not rejected: keys rotate and
                // the signature above already proved authenticity.
                keyId = kid;
            }

            var parsed = new LicensePayload(version, fingerprint, expiryDate, licensedTo, issuedAt, notBefore, keyId);
            if (!parsed.IsConsistent)
            {
                return false;
            }

            payload = parsed;
            reason = default;
            return true;
        }
    }

    private static bool TryReadString(
        IReadOnlyDictionary<string, JsonElement> fields,
        string name,
        bool required,
        out string? value)
    {
        value = null;
        if (!fields.TryGetValue(name, out var element))
        {
            return !required;
        }

        if (element.ValueKind == JsonValueKind.Null)
        {
            return !required;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString();
        return true;
    }

    private static void WriteJsonString(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (c < 0x20)
                    {
                        builder.Append("\\u00");
                        builder.Append(((int)c).ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        builder.Append('"');
    }
}
