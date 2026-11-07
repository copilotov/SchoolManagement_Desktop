using FluentAssertions;
using SchoolManagement.Domain.Licensing;

namespace SchoolManagement.Tests.Licensing;

public sealed class LicenseDateTests
{
    [Theory]
    [InlineData(2026, 1, 1, "2026-01-01")]
    [InlineData(2026, 12, 31, "2026-12-31")]
    [InlineData(999, 2, 3, "0999-02-03")]
    [InlineData(9999, 12, 31, "9999-12-31")]
    public void ToIso_pads_every_component(int year, int month, int day, string expected)
    {
        LicenseDate.ToIso(new DateOnly(year, month, day)).Should().Be(expected);
    }

    [Theory]
    [InlineData("2026-01-01", 2026, 1, 1)]
    [InlineData("2000-02-29", 2000, 2, 29)]
    [InlineData("2027-06-30", 2027, 6, 30)]
    public void TryParse_accepts_valid_calendar_days(string text, int year, int month, int day)
    {
        LicenseDate.TryParse(text, out var date).Should().BeTrue();
        date.Should().Be(new DateOnly(year, month, day));
    }

    [Theory]
    [InlineData("2026-1-01")]
    [InlineData("2026-01-1")]
    [InlineData("2026/01/01")]
    [InlineData("2026-01-01T00:00:00Z")]
    [InlineData("2026-13-01")]
    [InlineData("2026-00-10")]
    [InlineData("2026-01-00")]
    [InlineData("2026-01-32")]
    [InlineData("2023-02-29")]
    [InlineData("abcd-ef-gh")]
    [InlineData("")]
    public void TryParse_rejects_non_iso_or_impossible_dates(string text)
    {
        LicenseDate.TryParse(text, out _).Should().BeFalse();
    }

    [Fact]
    public void Parsed_dates_are_materialised_as_utc_midnight()
    {
        LicenseDate.TryParse("2026-03-10", out var date).Should().BeTrue();

        var utc = LicenseDate.ToUtcMidnight(date);

        utc.Kind.Should().Be(DateTimeKind.Utc);
        utc.Hour.Should().Be(0);
        utc.Minute.Should().Be(0);
        utc.Second.Should().Be(0);
        utc.Year.Should().Be(2026);
    }

    [Fact]
    public void Round_trip_is_stable()
    {
        var date = LicenseTestKit.Date(2031, 7, 9);
        LicenseDate.TryParse(LicenseDate.ToIso(date), out var parsed).Should().BeTrue();
        parsed.Should().Be(date);
    }
}

public sealed class Base64UrlTests
{
    [Fact]
    public void Encode_uses_the_url_safe_alphabet_and_keeps_padding()
    {
        var bytes = new byte[] { 0xfb, 0xff, 0xbf };

        // Standard alphabet would be "+/+/"; the URL-safe form uses - and _.
        Base64Url.Encode(bytes).Should().Be("-_-_");
    }

    [Fact]
    public void Encode_matches_dart_base64url_for_known_bytes()
    {
        // Dart: base64UrlEncode(utf8.encode('{"v":2}')) == 'eyJ2IjoyfQ=='
        Base64Url.Encode(System.Text.Encoding.UTF8.GetBytes("{\"v\":2}"))
            .Should().Be("eyJ2IjoyfQ==");
    }

    [Theory]
    [InlineData("eyJ2IjoyfQ==")]
    [InlineData("eyJ2IjoyfQ")]
    public void Decode_accepts_padded_and_unpadded_input(string value)
    {
        Base64Url.TryDecode(value, out var bytes).Should().BeTrue();
        System.Text.Encoding.UTF8.GetString(bytes).Should().Be("{\"v\":2}");
    }

    [Fact]
    public void Decode_accepts_both_url_safe_characters()
    {
        Base64Url.TryDecode("-_-_", out var bytes).Should().BeTrue();
        bytes.Should().Equal(0xfb, 0xff, 0xbf);
    }

    [Theory]
    [InlineData("abc$")]
    [InlineData("abc def")]
    [InlineData("a")]
    [InlineData("")]
    public void Decode_rejects_invalid_alphabet_or_impossible_length(string value)
    {
        Base64Url.TryDecode(value, out _).Should().BeFalse();
    }

    [Fact]
    public void Decode_rejects_null() => Base64Url.TryDecode(null, out _).Should().BeFalse();

    [Fact]
    public void Round_trip_arbitrary_bytes()
    {
        var bytes = Enumerable.Range(0, 64).Select(i => (byte)i).ToArray();
        Base64Url.TryDecode(Base64Url.Encode(bytes), out var decoded).Should().BeTrue();
        decoded.Should().Equal(bytes);
    }
}

public sealed class LicenseEnvelopeTests
{
    [Fact]
    public void Encode_then_decode_preserves_both_parts()
    {
        var payload = LicenseTestKit.Payload().WriteCanonicalJson();
        var signature = Enumerable.Repeat((byte)7, 64).ToArray();

        var envelope = LicenseEnvelope.Encode(payload, signature);

        LicenseEnvelope.TryDecode(envelope, out var payloadBytes, out var signatureBytes)
            .Should().BeTrue();
        System.Text.Encoding.UTF8.GetString(payloadBytes).Should().Be(payload);
        signatureBytes.Should().Equal(signature);
    }

    [Fact]
    public void Decode_trims_surrounding_whitespace()
    {
        LicenseEnvelope.TryDecode($"  {LicenseTestKit.LicenseB}\r\n", out _, out _)
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("only-one-part")]
    [InlineData(".b25seQ==")]
    [InlineData("b25seQ==.")]
    [InlineData("a.b.c")]
    [InlineData("bm90IGpzb24=.%%%")]
    public void Decode_rejects_structural_garbage(string value)
    {
        LicenseEnvelope.TryDecode(value, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Decode_rejects_null() => LicenseEnvelope.TryDecode(null, out _, out _).Should().BeFalse();

    [Fact]
    public void The_fixed_vector_is_a_two_part_envelope()
    {
        LicenseTestKit.LicenseA.Count(c => c == '.').Should().Be(1);
        LicenseEnvelope.TryDecode(LicenseTestKit.LicenseA, out _, out var signature)
            .Should().BeTrue();
        signature.Should().HaveCount(64);
    }
}

public sealed class DeviceFingerprintTests
{
    [Fact]
    public void Valid_hash_is_64_lowercase_hex_characters()
    {
        var fingerprint = new DeviceFingerprint(LicenseTestKit.Fingerprint, true, false);

        fingerprint.IsValid.Should().BeTrue();
        fingerprint.ShortCode.Should().Be("0123-4567-89AB-CDEF");
    }

    [Theory]
    [InlineData("")]
    [InlineData("0123")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcde")]
    [InlineData("0123456789ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdeg")]
    public void Invalid_hashes_are_reported(string hash)
    {
        new DeviceFingerprint(hash, true, false).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Short_code_degrades_gracefully_for_short_input()
    {
        new DeviceFingerprint("abc", true, false).ShortCode.Should().Be("abc");
        new DeviceFingerprint("", true, false).ShortCode.Should().BeEmpty();
    }

    [Fact]
    public void Fallback_flag_is_preserved()
    {
        var fingerprint = new DeviceFingerprint(LicenseTestKit.Fingerprint, false, true);

        fingerprint.IsStrong.Should().BeFalse();
        fingerprint.UsedFallback.Should().BeTrue();
    }
}
