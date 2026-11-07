using System.Globalization;
using System.Text;
using NSec.Cryptography;
using SchoolManagement.Domain.Licensing;

namespace SchoolManagement.LicenseTool;

/// <summary>
/// Offline license issuer. This project is not referenced by the desktop app
/// and is never shipped to customers: it is the only place that handles the
/// Ed25519 private key, which must stay outside the repository.
///
/// Commands:
///   keygen --out &lt;private-key-file&gt; [--force]
///   issue  --key &lt;private-key-file&gt; --fp &lt;64-hex&gt; --exp yyyy-MM-dd
///          [--iat yyyy-MM-dd] [--nbf yyyy-MM-dd] [--to "School name"]
///          [--kid k1] [--v 1|2] [--verbose]
///   verify --pub &lt;base64&gt; --license &lt;text&gt; --fp &lt;64-hex&gt;
/// </summary>
public static class Program
{
    private static readonly SignatureAlgorithm Algorithm = SignatureAlgorithm.Ed25519;

    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            return Usage();
        }

        var options = ParseOptions(args.Skip(1));
        if (options is null)
        {
            return Usage();
        }

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "keygen" => Keygen(options),
                "issue" => Issue(options),
                "verify" => Verify(options),
                _ => Usage(),
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"error: {exception.Message}");
            return 1;
        }
    }

    private static int Keygen(IReadOnlyDictionary<string, string> options)
    {
        var output = Required(options, "out");
        var force = options.ContainsKey("force");
        if (File.Exists(output) && !force)
        {
            Console.Error.WriteLine($"error: '{output}' already exists; pass --force to overwrite it.");
            return 1;
        }

        var creation = new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport };
        using var key = Key.Create(Algorithm, creation);

        var privateBytes = key.Export(KeyBlobFormat.RawPrivateKey);
        var publicBytes = key.PublicKey.Export(KeyBlobFormat.RawPublicKey);

        var directory = Path.GetDirectoryName(Path.GetFullPath(output));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(output, Convert.ToBase64String(privateBytes), Encoding.ASCII);

        Console.WriteLine($"public (base64): {Convert.ToBase64String(publicBytes)}");
        Console.WriteLine($"public (hex)   : {Convert.ToHexString(publicBytes).ToLowerInvariant()}");
        Console.Error.WriteLine($"private key written to '{output}'. Keep it out of the repository and back it up.");
        return 0;
    }

    private static int Issue(IReadOnlyDictionary<string, string> options)
    {
        var keyPath = Required(options, "key");
        var fingerprint = Required(options, "fp").ToLowerInvariant();
        var expiry = ParseDate(Required(options, "exp"), "exp");
        var issuedAt = options.TryGetValue("iat", out var iatText)
            ? ParseDate(iatText, "iat")
            : DateOnly.FromDateTime(DateTime.UtcNow);
        var notBefore = options.TryGetValue("nbf", out var nbfText) ? ParseDate(nbfText, "nbf") : (DateOnly?)null;
        var licensedTo = options.TryGetValue("to", out var toText) ? toText : null;
        var keyId = options.TryGetValue("kid", out var kidText) && !string.IsNullOrWhiteSpace(kidText)
            ? kidText
            : LicensePayload.DefaultKeyId;
        var version = options.TryGetValue("v", out var versionText)
            ? int.Parse(versionText, CultureInfo.InvariantCulture)
            : LicensePayload.CurrentVersion;

        if (notBefore is { } nbf && nbf > expiry)
        {
            Console.Error.WriteLine("error: --nbf is after --exp; the app rejects that license as malformed.");
            return 1;
        }

        var payload = new LicensePayload(version, fingerprint, expiry, licensedTo, issuedAt, notBefore, keyId);
        var payloadJson = payload.WriteCanonicalJson();
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);

        var privateBytes = Convert.FromBase64String(File.ReadAllText(keyPath).Trim());
        using var key = Key.Import(Algorithm, privateBytes, KeyBlobFormat.RawPrivateKey);
        var signature = Algorithm.Sign(key, payloadBytes);

        if (options.ContainsKey("verbose"))
        {
            Console.Error.WriteLine($"payload: {payloadJson}");
        }

        Console.WriteLine(LicenseEnvelope.Encode(payloadJson, signature));
        return 0;
    }

    private static int Verify(IReadOnlyDictionary<string, string> options)
    {
        var publicKey = Convert.FromBase64String(Required(options, "pub"));
        var license = Required(options, "license");
        var fingerprint = Required(options, "fp");

        if (!LicenseEnvelope.TryDecode(license, out var payloadBytes, out var signatureBytes))
        {
            Console.WriteLine("result: Malformed (envelope)");
            return 2;
        }

        var key = PublicKey.Import(Algorithm, publicKey, KeyBlobFormat.RawPublicKey);
        if (!Algorithm.Verify(key, payloadBytes, signatureBytes))
        {
            Console.WriteLine("result: BadSignature");
            return 2;
        }

        if (!LicensePayload.TryParse(payloadBytes, out var payload, out var reason))
        {
            Console.WriteLine($"result: {reason}");
            return 2;
        }

        if (!string.Equals(payload!.Fingerprint, fingerprint, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("result: FingerprintMismatch");
            return 2;
        }

        Console.WriteLine($"result: Valid (v{payload.Version}, exp {LicenseDate.ToIso(payload.ExpiryDate)})");
        return 0;
    }

    private static DateOnly ParseDate(string text, string option)
    {
        if (!LicenseDate.TryParse(text, out var date))
        {
            throw new FormatException($"--{option} must be yyyy-MM-dd");
        }

        return date;
    }

    private static string Required(IReadOnlyDictionary<string, string> options, string name) =>
        options.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"missing --{name}");

    private static Dictionary<string, string>? ParseOptions(IEnumerable<string> args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var enumerator = args.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var current = enumerator.Current;
            if (!current.StartsWith("--", StringComparison.Ordinal))
            {
                return null;
            }

            var name = current[2..];
            if (name is "force" or "verbose")
            {
                options[name] = "true";
                continue;
            }

            if (!enumerator.MoveNext())
            {
                return null;
            }

            options[name] = enumerator.Current;
        }

        return options;
    }

    private static int Usage()
    {
        Console.Error.WriteLine(
            """
            SchoolManagement.LicenseTool

              keygen --out <private-key-file> [--force]
              issue  --key <private-key-file> --fp <64-hex> --exp yyyy-MM-dd
                     [--iat yyyy-MM-dd] [--nbf yyyy-MM-dd] [--to "School name"]
                     [--kid k1] [--v 1|2] [--verbose]
              verify --pub <base64> --license <text> --fp <64-hex>
            """);
        return 1;
    }
}
