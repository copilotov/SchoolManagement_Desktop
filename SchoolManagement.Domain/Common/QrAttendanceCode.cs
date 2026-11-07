namespace SchoolManagement.Domain.Common;

/// <summary>
/// Payload of the student QR code used by the attendance scanner.
/// The code is <c>CODE:&lt;studentCode&gt;</c>, for example
/// <c>CODE:STU-2026-0007</c>. Parsing ignores case around the prefix and
/// surrounding whitespace because a webcam read and a USB barcode reader can
/// both add noise.
/// </summary>
public static class QrAttendanceCode
{
    public const string Prefix = "CODE:";

    public static string Format(string studentCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(studentCode);
        return Prefix + studentCode.Trim();
    }

    /// <summary>Strict parser: the payload must carry the <c>CODE:</c> prefix.</summary>
    public static bool TryParse(string? payload, out string studentCode)
    {
        studentCode = string.Empty;
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        var trimmed = payload.Trim();
        if (!trimmed.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var value = trimmed[Prefix.Length..].Trim();
        if (value.Length == 0 || !IsPlausibleStudentCode(value))
        {
            return false;
        }

        studentCode = value;
        return true;
    }

    /// <summary>
    /// Tolerant parser used by the manual capture field and the USB reader:
    /// accepts a full <c>CODE:</c> payload as well as a bare student code.
    /// </summary>
    public static bool TryParseInput(string? input, out string studentCode)
    {
        if (TryParse(input, out studentCode))
        {
            return true;
        }

        studentCode = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var value = input.Trim();
        if (!IsPlausibleStudentCode(value))
        {
            return false;
        }

        studentCode = value;
        return true;
    }

    private static bool IsPlausibleStudentCode(string value)
    {
        foreach (var character in value)
        {
            if (!char.IsLetterOrDigit(character) && character != '-' && character != '_')
            {
                return false;
            }
        }

        return true;
    }
}
