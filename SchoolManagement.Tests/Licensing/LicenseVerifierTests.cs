using System.Text;
using FluentAssertions;
using SchoolManagement.Domain.Licensing;
using SchoolManagement.Infrastructure.Licensing.Ed25519;

namespace SchoolManagement.Tests.Licensing;

public sealed class LicenseVerifierTests
{
    private readonly LicenseVerifier _verifier = LicenseTestKit.Verifier();

    [Fact]
    public void The_fixed_tool_vector_verifies()
    {
        var result = _verifier.Verify(LicenseTestKit.LicenseA, LicenseTestKit.Fingerprint);

        result.IsValid.Should().BeTrue();
        result.FailureReason.Should().BeNull();
        result.Payload.Should().NotBeNull();
        result.Payload!.LicensedTo.Should().Be("مدرسة النور");
        result.PayloadJson.Should().Be(result.Payload.WriteCanonicalJson());
    }

    [Fact]
    public void A_license_without_optional_members_verifies()
    {
        var result = _verifier.Verify(LicenseTestKit.LicenseB, LicenseTestKit.Fingerprint);

        result.IsValid.Should().BeTrue();
        result.Payload!.LicensedTo.Should().BeNull();
        result.Payload.NotBefore.Should().BeNull();
    }

    [Fact]
    public void Freshly_signed_payloads_verify()
    {
        var license = LicenseTestKit.Sign(LicenseTestKit.Payload(
            expiry: new DateOnly(2030, 12, 31),
            notBefore: new DateOnly(2026, 3, 1)));

        var result = _verifier.Verify(license, LicenseTestKit.Fingerprint);

        result.IsValid.Should().BeTrue();
        result.Payload!.ExpiryDate.Should().Be(new DateOnly(2030, 12, 31));
    }

    [Fact]
    public void Surrounding_whitespace_from_a_paste_is_tolerated()
    {
        _verifier.Verify($"\r\n  {LicenseTestKit.LicenseA}  \n", LicenseTestKit.Fingerprint)
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void The_fingerprint_comparison_is_case_insensitive()
    {
        _verifier.Verify(LicenseTestKit.LicenseA, LicenseTestKit.Fingerprint.ToUpperInvariant())
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_fingerprint_with_whitespace_is_trimmed()
    {
        _verifier.Verify(LicenseTestKit.LicenseA, $" {LicenseTestKit.Fingerprint} ")
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void Another_device_is_a_fingerprint_mismatch()
    {
        var result = _verifier.Verify(LicenseTestKit.LicenseA, LicenseTestKit.OtherFingerprint);

        result.IsValid.Should().BeFalse();
        result.FailureReason.Should().Be(LicenseFailureReason.FingerprintMismatch);
        result.Payload.Should().BeNull();
    }

    [Fact]
    public void Flipping_one_payload_byte_breaks_the_signature()
    {
        LicenseEnvelope.TryDecode(LicenseTestKit.LicenseA, out var payload, out var signature)
            .Should().BeTrue();
        payload[10] ^= 0x01;

        var tampered = LicenseEnvelope.Encode(Encoding.UTF8.GetString(payload), signature);

        var result = _verifier.Verify(tampered, LicenseTestKit.Fingerprint);
        result.IsValid.Should().BeFalse();
        result.FailureReason.Should().Be(LicenseFailureReason.BadSignature);
    }

    [Fact]
    public void Flipping_one_signature_byte_is_a_bad_signature()
    {
        LicenseEnvelope.TryDecode(LicenseTestKit.LicenseA, out var payload, out var signature)
            .Should().BeTrue();
        signature[0] ^= 0x01;

        var result = _verifier.Verify(
            LicenseEnvelope.Encode(Encoding.UTF8.GetString(payload), signature),
            LicenseTestKit.Fingerprint);

        result.FailureReason.Should().Be(LicenseFailureReason.BadSignature);
    }

    [Fact]
    public void A_signature_from_another_key_is_rejected()
    {
        var foreign = LicenseTestKit.Sign(LicenseTestKit.Payload(), LicenseTestKit.OtherPrivateKey);

        var result = _verifier.Verify(foreign, LicenseTestKit.Fingerprint);

        result.IsValid.Should().BeFalse();
        result.FailureReason.Should().Be(LicenseFailureReason.BadSignature);
    }

    [Fact]
    public void A_truncated_signature_is_rejected()
    {
        LicenseEnvelope.TryDecode(LicenseTestKit.LicenseA, out var payload, out var signature)
            .Should().BeTrue();

        var truncated = LicenseEnvelope.Encode(
            Encoding.UTF8.GetString(payload),
            signature.AsSpan(0, 63).ToArray());

        _verifier.Verify(truncated, LicenseTestKit.Fingerprint).FailureReason
            .Should().Be(LicenseFailureReason.BadSignature);
    }

    [Fact]
    public void Pasting_a_different_payload_under_the_original_signature_is_rejected()
    {
        LicenseEnvelope.TryDecode(LicenseTestKit.LicenseA, out _, out var signature).Should().BeTrue();
        var replacement = LicenseTestKit.Payload(expiry: new DateOnly(2099, 12, 31)).WriteCanonicalJson();

        var forged = LicenseEnvelope.Encode(replacement, signature);

        _verifier.Verify(forged, LicenseTestKit.Fingerprint).FailureReason
            .Should().Be(LicenseFailureReason.BadSignature);
    }

    [Fact]
    public void Signature_is_checked_before_the_version_policy()
    {
        // A payload claiming an unsupported version, but NOT signed by us, must
        // be reported as a bad signature, not as "update the app".
        var unsignedV3 = "{\"v\":3,\"fp\":\"" + LicenseTestKit.Fingerprint +
                          "\",\"exp\":\"2027-01-31\",\"to\":null,\"iat\":\"2026-01-01\",\"kid\":\"k1\"}";
        var forged = LicenseEnvelope.Encode(unsignedV3, new byte[64]);

        var result = _verifier.Verify(forged, LicenseTestKit.Fingerprint);

        result.FailureReason.Should().Be(LicenseFailureReason.BadSignature);
    }

    [Fact]
    public void A_correctly_signed_unsupported_version_reports_the_version_policy()
    {
        var result = _verifier.Verify(LicenseTestKit.LicenseVersion3, LicenseTestKit.Fingerprint);

        result.IsValid.Should().BeFalse();
        result.FailureReason.Should().Be(LicenseFailureReason.UnsupportedVersion);
    }

    [Fact]
    public void A_correctly_signed_malformed_payload_is_malformed()
    {
        var signed = LicenseTestKit.SignJson("{\"v\":1,\"fp\":\"x\"}");

        _verifier.Verify(signed, "x").FailureReason.Should().Be(LicenseFailureReason.Malformed);
    }

    [Fact]
    public void A_correctly_signed_not_before_after_expiry_is_malformed()
    {
        var signed = LicenseTestKit.SignJson(
            "{\"v\":2,\"fp\":\"" + LicenseTestKit.Fingerprint +
            "\",\"exp\":\"2026-01-01\",\"to\":null,\"iat\":\"2025-01-01\",\"nbf\":\"2026-02-01\",\"kid\":\"k1\"}");

        _verifier.Verify(signed, LicenseTestKit.Fingerprint).FailureReason
            .Should().Be(LicenseFailureReason.Malformed);
    }

    [Fact]
    public void The_signature_covers_raw_bytes_not_a_re_serialisation()
    {
        // Different member order and whitespace: not canonical, but signed as-is.
        var json = "{ \"kid\" : \"k1\", \"v\" : 2, \"fp\" : \"" + LicenseTestKit.Fingerprint +
                   "\", \"exp\" : \"2027-06-30\", \"to\" : null, \"iat\" : \"2026-01-01\" }";

        _verifier.Verify(LicenseTestKit.SignJson(json), LicenseTestKit.Fingerprint)
            .IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("garbage")]
    [InlineData("a.b.c")]
    public void Empty_or_garbage_text_is_malformed(string value)
    {
        _verifier.Verify(value, LicenseTestKit.Fingerprint).FailureReason
            .Should().Be(LicenseFailureReason.Malformed);
    }

    [Fact]
    public void Null_text_is_malformed() =>
        _verifier.Verify(null, LicenseTestKit.Fingerprint).FailureReason
            .Should().Be(LicenseFailureReason.Malformed);

    [Fact]
    public void An_unconfigured_verifier_reports_not_configured()
    {
        var verifier = new LicenseVerifier(Array.Empty<byte>());

        verifier.IsConfigured.Should().BeFalse();
        verifier.Verify(LicenseTestKit.LicenseA, LicenseTestKit.Fingerprint).FailureReason
            .Should().Be(LicenseFailureReason.NotConfigured);
    }

    [Fact]
    public void The_default_constructor_uses_the_shipped_public_key()
    {
        var verifier = new LicenseVerifier();

        verifier.IsConfigured.Should().BeTrue();
        verifier.Verify(LicenseTestKit.LicenseA, LicenseTestKit.Fingerprint).FailureReason
            .Should().Be(LicenseFailureReason.BadSignature);
        LicenseReadOnlyConventions.PublicKeyIsCompileTimeConstant().Should().BeTrue();
    }

    [Fact]
    public void A_non_base64_public_key_is_treated_as_unconfigured()
    {
        var verifier = new LicenseVerifier("not base64!");

        verifier.IsConfigured.Should().BeFalse();
        verifier.Verify(LicenseTestKit.LicenseA, LicenseTestKit.Fingerprint).FailureReason
            .Should().Be(LicenseFailureReason.NotConfigured);
    }
}

/// <summary>
/// Guards two invariants of the trust root: it is a compile-time constant (no
/// environment variable can replace it) and it is a 32-byte Ed25519 key.
/// </summary>
internal static class LicenseReadOnlyConventions
{
    public static bool PublicKeyIsCompileTimeConstant()
    {
        var field = typeof(LicensePublicKey).GetField(nameof(LicensePublicKey.Base64));
        var key = LicensePublicKey.ToArray();

        return field is { IsLiteral: true } &&
               key.Length == 32 &&
               LicensePublicKey.ToArray() != key;
    }
}
