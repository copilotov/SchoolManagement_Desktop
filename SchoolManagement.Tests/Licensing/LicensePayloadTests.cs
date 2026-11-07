using System.Text;
using FluentAssertions;
using SchoolManagement.Domain.Licensing;

namespace SchoolManagement.Tests.Licensing;

public sealed class LicensePayloadCanonicalTests
{
    private static readonly DateOnly Expiry = new(2027, 6, 30);

    [Fact]
    public void Full_payload_has_the_exact_dart_compatible_shape()
    {
        var payload = new LicensePayload(
            2,
            LicenseTestKit.Fingerprint,
            Expiry,
            "مدرسة النور",
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 2, 1),
            "k1");

        payload.WriteCanonicalJson().Should().Be(
            "{\"v\":2,\"fp\":\"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\"," +
            "\"exp\":\"2027-06-30\",\"to\":\"مدرسة النور\",\"iat\":\"2026-01-01\"," +
            "\"nbf\":\"2026-02-01\",\"kid\":\"k1\"}");
    }

    [Fact]
    public void Null_licensed_to_is_written_as_json_null()
    {
        var payload = LicenseTestKit.Payload(licensedTo: null).WriteCanonicalJson();

        payload.Should().Contain("\"to\":null");
        payload.Should().NotContain("\"nbf\"");
    }

    [Fact]
    public void Missing_not_before_omits_the_nbf_member_entirely()
    {
        LicenseTestKit.Payload(notBefore: null).WriteCanonicalJson()
            .Should().NotContain("\"nbf\"");
    }

    [Fact]
    public void Present_not_before_adds_the_nbf_member_between_iat_and_kid()
    {
        var json = LicenseTestKit.Payload(notBefore: new DateOnly(2026, 2, 1))
            .WriteCanonicalJson();

        json.Should().Contain("\"iat\":\"2026-01-01\",\"nbf\":\"2026-02-01\",\"kid\":\"k1\"");
    }

    [Fact]
    public void Member_order_is_always_identical()
    {
        var json = LicenseTestKit.Payload(
                licensedTo: "School",
                notBefore: new DateOnly(2026, 2, 1))
            .WriteCanonicalJson();

        var order = new[] { "\"v\"", "\"fp\"", "\"exp\"", "\"to\"", "\"iat\"", "\"nbf\"", "\"kid\"" }
            .Select(member => json.IndexOf(member, StringComparison.Ordinal))
            .ToArray();

        order.Should().BeInAscendingOrder();
        order.Should().OnlyContain(index => index >= 0);
    }

    [Fact]
    public void There_is_no_whitespace_between_members()
    {
        LicenseTestKit.Payload(licensedTo: "School").WriteCanonicalJson()
            .Should().NotContain(" ").And.NotContain("\n").And.NotContain("\r");
    }

    [Fact]
    public void Non_ascii_is_written_raw_utf8_not_escaped()
    {
        var json = LicenseTestKit.Payload(licensedTo: "مدرسة النور").WriteCanonicalJson();

        json.Should().Contain("\"to\":\"مدرسة النور\"");
        json.Should().NotContain("\\u");
    }

    [Fact]
    public void Quotes_backslashes_and_control_characters_are_escaped_like_dart()
    {
        var json = LicenseTestKit.Payload(licensedTo: "A\"B\\C\nD\tE\u0001F").WriteCanonicalJson();

        json.Should().Contain("\"to\":\"A\\\"B\\\\C\\nD\\tE\\u0001F\"");
    }

    [Fact]
    public void Signed_bytes_are_exactly_the_canonical_json()
    {
        var payload = LicenseTestKit.Payload(licensedTo: "مدرسة النور");
        var canonical = payload.WriteCanonicalJson();

        var license = LicenseTestKit.Sign(payload);
        LicenseEnvelope.TryDecode(license, out var payloadBytes, out _).Should().BeTrue();

        Encoding.UTF8.GetString(payloadBytes).Should().Be(canonical);
        Base64Url.Encode(payloadBytes).Should().Be(license[..license.IndexOf('.')]);
    }

    [Fact]
    public void The_tool_generated_vector_matches_this_writer_byte_for_byte()
    {
        var payload = new LicensePayload(
            2,
            LicenseTestKit.Fingerprint,
            new DateOnly(2027, 6, 30),
            "مدرسة النور",
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 2, 1),
            "k1");

        LicenseTestKit.Sign(payload).Should().Be(LicenseTestKit.LicenseA);
    }
}

public sealed class LicensePayloadParserTests
{
    private static bool TryParse(string json, out LicensePayload? payload, out LicenseFailureReason reason) =>
        LicensePayload.TryParse(Encoding.UTF8.GetBytes(json), out payload, out reason);

    private static string Json(
        int version = 2,
        string fingerprint = LicenseTestKit.Fingerprint,
        string expiry = "2027-06-30",
        string licensedTo = "\"School\"",
        string issuedAt = "2026-01-01",
        string? notBefore = null,
        string keyId = "\"k1\"") =>
        $"{{\"v\":{version},\"fp\":\"{fingerprint}\",\"exp\":\"{expiry}\",\"to\":{licensedTo}," +
        $"\"iat\":\"{issuedAt}\"" +
        (notBefore is null ? string.Empty : $",\"nbf\":\"{notBefore}\"") +
        $",\"kid\":{keyId}}}";

    [Fact]
    public void Parses_the_tool_generated_payload()
    {
        LicenseEnvelope.TryDecode(LicenseTestKit.LicenseA, out var bytes, out _).Should().BeTrue();

        LicensePayload.TryParse(bytes, out var payload, out var reason).Should().BeTrue();

        reason.Should().Be(default(LicenseFailureReason));
        payload.Should().NotBeNull();
        payload!.Version.Should().Be(2);
        payload.Fingerprint.Should().Be(LicenseTestKit.Fingerprint);
        payload.ExpiryDate.Should().Be(new DateOnly(2027, 6, 30));
        payload.LicensedTo.Should().Be("مدرسة النور");
        payload.IssuedAt.Should().Be(new DateOnly(2026, 1, 1));
        payload.NotBefore.Should().Be(new DateOnly(2026, 2, 1));
        payload.KeyId.Should().Be("k1");
    }

    [Fact]
    public void Version_1_is_still_accepted() =>
        TryParse(Json(version: 1), out var payload, out _).Should().BeTrue();

    [Fact]
    public void Version_0_is_malformed()
    {
        TryParse(Json(version: 0), out _, out var reason).Should().BeFalse();
        reason.Should().Be(LicenseFailureReason.Malformed);
    }

    [Fact]
    public void Version_above_current_is_unsupported_not_malformed()
    {
        TryParse(Json(version: 3), out _, out var reason).Should().BeFalse();
        reason.Should().Be(LicenseFailureReason.UnsupportedVersion);
    }

    [Fact]
    public void Missing_version_is_malformed()
    {
        TryParse("{\"fp\":\"x\",\"exp\":\"2027-06-30\",\"to\":null,\"iat\":\"2026-01-01\"}", out _, out var reason)
            .Should().BeFalse();
        reason.Should().Be(LicenseFailureReason.Malformed);
    }

    [Fact]
    public void Duplicate_members_are_rejected()
    {
        var json = Json().Replace("{\"v\":2", "{\"v\":2,\"v\":2", StringComparison.Ordinal);
        TryParse(json, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Missing_fingerprint_is_malformed()
    {
        TryParse("{\"v\":2,\"exp\":\"2027-06-30\",\"to\":null,\"iat\":\"2026-01-01\"}", out _, out _)
            .Should().BeFalse();
    }

    [Fact]
    public void Blank_fingerprint_is_malformed() =>
        TryParse(Json(fingerprint: "   "), out _, out _).Should().BeFalse();

    [Fact]
    public void Non_string_fingerprint_is_malformed()
    {
        TryParse("{\"v\":2,\"fp\":42,\"exp\":\"2027-06-30\",\"to\":null,\"iat\":\"2026-01-01\"}", out _, out _)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData("2027-6-30")]
    [InlineData("30/06/2027")]
    [InlineData("not-a-date")]
    [InlineData("2027-02-30")]
    public void Bad_expiry_is_malformed(string expiry) =>
        TryParse(Json(expiry: expiry), out _, out _).Should().BeFalse();

    [Fact]
    public void Missing_licensed_to_member_is_malformed()
    {
        TryParse("{\"v\":2,\"fp\":\"x\",\"exp\":\"2027-06-30\",\"iat\":\"2026-01-01\"}", out _, out _)
            .Should().BeFalse();
    }

    [Fact]
    public void Empty_licensed_to_becomes_null()
    {
        TryParse(Json(licensedTo: "\"\""), out var payload, out _).Should().BeTrue();
        payload!.LicensedTo.Should().BeNull();
    }

    [Fact]
    public void Null_licensed_to_is_accepted()
    {
        TryParse(Json(licensedTo: "null"), out var payload, out _).Should().BeTrue();
        payload!.LicensedTo.Should().BeNull();
    }

    [Fact]
    public void Numeric_licensed_to_is_malformed() =>
        TryParse(Json(licensedTo: "12"), out _, out _).Should().BeFalse();

    [Fact]
    public void Bad_issued_at_is_malformed() =>
        TryParse(Json(issuedAt: "2026-13-01"), out _, out _).Should().BeFalse();

    [Fact]
    public void Null_not_before_is_accepted_as_absent()
    {
        var json = Json().Replace(",\"kid\":\"k1\"", ",\"nbf\":null,\"kid\":\"k1\"", StringComparison.Ordinal);

        TryParse(json, out var payload, out _).Should().BeTrue();
        payload!.NotBefore.Should().BeNull();
    }

    [Fact]
    public void Invalid_not_before_is_malformed() =>
        TryParse(Json(notBefore: "2026-02-30"), out _, out _).Should().BeFalse();

    [Fact]
    public void Not_before_after_expiry_is_malformed()
    {
        TryParse(Json(expiry: "2026-01-31", notBefore: "2026-02-01"), out _, out var reason)
            .Should().BeFalse();
        reason.Should().Be(LicenseFailureReason.Malformed);
    }

    [Fact]
    public void Not_before_on_the_expiry_day_is_accepted()
    {
        TryParse(Json(expiry: "2026-02-01", notBefore: "2026-02-01"), out var payload, out _)
            .Should().BeTrue();
        payload!.IsConsistent.Should().BeTrue();
    }

    [Fact]
    public void Missing_kid_falls_back_to_the_default()
    {
        var json = Json().Replace(",\"kid\":\"k1\"", string.Empty, StringComparison.Ordinal);

        TryParse(json, out var payload, out _).Should().BeTrue();
        payload!.KeyId.Should().Be(LicensePayload.DefaultKeyId);
    }

    [Fact]
    public void Unknown_kid_is_preserved_because_the_signature_already_proved_authenticity()
    {
        TryParse(Json(keyId: "\"rotated-2030\""), out var payload, out _).Should().BeTrue();
        payload!.KeyId.Should().Be("rotated-2030");
    }

    [Fact]
    public void Unknown_extra_members_are_ignored()
    {
        var json = Json().Replace("{\"v\":2", "{\"future\":true,\"v\":2", StringComparison.Ordinal);

        TryParse(json, out var payload, out _).Should().BeTrue();
        payload!.Version.Should().Be(2);
    }

    [Fact]
    public void A_json_array_is_malformed() =>
        TryParse("[1,2,3]", out _, out _).Should().BeFalse();

    [Fact]
    public void Empty_input_is_malformed()
    {
        LicensePayload.TryParse(ReadOnlySpan<byte>.Empty, out _, out var reason).Should().BeFalse();
        reason.Should().Be(LicenseFailureReason.Malformed);
    }

    [Fact]
    public void Invalid_utf8_or_truncated_json_is_malformed() =>
        TryParse("{\"v\":2,", out _, out _).Should().BeFalse();

    [Fact]
    public void Trailing_commas_are_rejected() =>
        TryParse(Json().Replace("}", ",}", StringComparison.Ordinal), out _, out _).Should().BeFalse();

    [Fact]
    public void Comments_are_rejected() =>
        TryParse("/*x*/" + Json(), out _, out _).Should().BeFalse();
}
