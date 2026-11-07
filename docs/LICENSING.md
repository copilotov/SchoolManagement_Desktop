# Licensing (Phase 3)

The desktop app is offline. A license is a signed text string that the customer
receives from the provider and pastes into the activation screen. The app
verifies it locally with an Ed25519 public key baked into the build; there is no
server call.

## 1. License format

```
base64url(payload_json) + "." + base64url(ed25519_signature)
```

- `payload_json` is UTF-8, **not** canonicalised by the verifier: the signature
  covers the exact bytes that were signed.
- Both parts use the URL-safe base64 alphabet (`-` and `_`). The issuer keeps
  the `=` padding, matching Dart's `base64UrlEncode`. The verifier accepts
  padded and unpadded input because users copy keys by hand.
- The signature is 64 raw bytes.

### Payload

```json
{"v":2,"fp":"<64 lowercase hex>","exp":"2027-06-30","to":"مدرسة النور","iat":"2026-01-01","nbf":"2026-02-01","kid":"k1"}
```

| Field | Required | Meaning |
| --- | --- | --- |
| `v` | yes | Payload version. `1` and `2` are accepted, `>2` is `UnsupportedVersion`, `<1` is `Malformed`. |
| `fp` | yes | SHA-256 fingerprint of the device, 64 hex chars. |
| `exp` | yes | Last valid day, inclusive, `yyyy-MM-dd`. |
| `to` | yes | Licensed customer name, string or `null`. An empty string is read as `null`. |
| `iat` | yes | Issue day. Used as the trusted-clock anchor. |
| `nbf` | no | First valid day, inclusive. Omitted means "immediately". `nbf > exp` is `Malformed`. |
| `kid` | no | Key identifier. Missing defaults to `k1`; an unknown value is accepted because the signature already proved authenticity. |

Member order is fixed (`v, fp, exp, to, iat, [nbf], kid`) with compact
separators. Non-ASCII characters are written raw (UTF-8), not `\u` escaped;
only `"`, `\`, and control characters are escaped, exactly like Dart's
`jsonEncode`.

> `LicensePayload.WriteCanonicalJson` in `SchoolManagement.Domain/Licensing`
> is signature-sensitive. Re-ordering or pretty-printing it would invalidate
> every license produced by the phone tool. The fixed vectors in
> `SchoolManagement.Tests/Licensing/LicenseTestKit.cs` protect against that.

### The seven states

All boundaries are inclusive calendar days, evaluated against the trusted date:

| State | Window | Writing |
| --- | --- | --- |
| `Active` | more than 30 days before `exp` | allowed |
| `ExpiringSoon` | `exp - 30` … `exp` | allowed |
| `GracePeriod` | `exp + 1` … `exp + 30` | allowed, banner shows days left and the read-only date |
| `ReadOnly` | `exp + 31` and later | blocked |
| `NotStarted` | before `nbf` | blocked |
| `Unlicensed` | no license stored | blocked |
| `Invalid` | stored license failed verification | blocked |

`nbf` day itself is **not** `NotStarted`. The first screen rule is separate
from writing: only `Unlicensed` and `Invalid` open the activation page; the
other five states open the shell (read-only where applicable). That decision is
`LicenseInfo.RequiresActivation`, never `CanWrite`.

## 2. Trusted time

`ITrustedClock` is the only source of "now" for license validity. It merges
three values in `TrustedTimeEngine`:

1. the device clock (`ILocalClock`, the only place that calls
   `DateTime.Now`/`UtcNow`, in `SystemLocalClock`);
2. the highest instant ever observed, persisted encrypted in
   `trusted_time.dat`;
3. the license `iat` anchor, which exists even before the first activation.

Rules:

- the trusted instant is never lowered;
- a device more than **5 minutes** behind the stored instant is a rollback;
- the anchor is compared by **local calendar date with a one-day grace**, so a
  time-zone change (at most ±1 day from UTC) is not mistaken for tampering;
- a stored rollback flag stays until the clock is correct again;
- `iat`/stored values before the year 2000 are treated as corrupt and ignored.

A detected rollback keeps the last trusted date, shows the tamper banner, and
does not extend the license. This is an honest, offline deterrent, not an
unbreakable DRM: a determined user who wipes the DPAPI files can still reset
local state, but they cannot forge a license without the private key.

## 3. Where state lives

| File | Protection | Contents |
| --- | --- | --- |
| `%LOCALAPPDATA%\SchoolManagement\license.dat` | DPAPI, `CurrentUser` | the raw license text |
| `%LOCALAPPDATA%\SchoolManagement\trusted_time.dat` | DPAPI, `CurrentUser` | JSON `{lastKnown, observedAt, rollback}` |

`CurrentUser` is deliberate: a school PC is used by one Windows account.
`LocalMachine` would allow any local account to read or copy the licence. A
missing or unreadable file means "no license". A present but invalid license is
never deleted by reading it, so a key issued for another device is not lost when
the user retries. Only an explicit deactivation clears it.

## 4. Issuer tool

The signing tool is `tools/SchoolManagement.LicenseTool`. It is **not**
referenced by any shipped project; only the 32-byte public key is compiled into
`SchoolManagement.Infrastructure/Licensing/Ed25519/LicensePublicKey.cs`.

```powershell
# 1. One-time key generation (private key outside the repository!)
dotnet run --project tools\SchoolManagement.LicenseTool -- keygen `
  --out "$env:LOCALAPPDATA\SchoolManagement\license_private.key"

# The command prints the matching public key. Paste it into LicensePublicKey.Base64
# and publish a new build. Never commit the private key.

# 2. Issue a license for the fingerprint shown on the activation screen.
dotnet run --project tools\SchoolManagement.LicenseTool -- issue `
  --key "$env:LOCALAPPDATA\SchoolManagement\license_private.key" `
  --fp 0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef `
  --exp 2027-06-30 --to "مدرسة النور" --iat 2026-01-01 [--nbf 2026-02-01] [--kid k1]

# 3. Check a key before sending it.
dotnet run --project tools\SchoolManagement.LicenseTool -- verify `
  --pub <base64-public-key> --license <key> --fp <fingerprint>
```

`issue` writes the same bytes as the phone's `tool/license_tool.dart`:
compact payload JSON in the order above, padded base64url, Ed25519 over the raw
payload bytes. `--verbose` prints the payload to stderr for diagnostics.

### Fixed test vectors

`LicenseTestKit` embeds two licenses produced by this tool plus a correctly
signed `v3` license, together with *test-only* key pairs. They are not
production secrets. The tests assert that the canonical writer reproduces the
exact license string, so a format regression fails before it reaches a customer.

## 5. Build note

`TreatWarningsAsErrors` applies to the libraries; the tool and tests build
in the same solution. The solution therefore needs a .NET 8 SDK even on a
machine that only runs the published desktop build.

## 6. Known limits

- The fingerprint is `SHA-256(MachineGuid | BIOS serial | baseboard serial |
  disk serial)`. A Windows reinstall or a disk replacement changes it and needs
  a reissued key. When `MachineGuid` or all hardware serials are unreadable the
  fingerprint is flagged weak and the activation screen warns the user.
- Clock-rollback detection cannot stop a user who deletes both DPAPI files and
  reinstalls the app; it does stop the common "move the calendar back" trick.
- Signature verification happens before semantic parsing. A tampered payload is
  reported as `BadSignature`, not as a version or format problem.
- There is no online revocation in an offline product. Key rotation is handled
  by publishing a new build with a new `LicensePublicKey` and using `kid` to
  distinguish issuers.
