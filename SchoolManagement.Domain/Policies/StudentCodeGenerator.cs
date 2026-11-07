using System.Globalization;

namespace SchoolManagement.Domain.Policies;

/// <summary>
/// Builds the human-readable student code that is also the QR payload key.
/// Format: <c>STU-{year}-{sequence:0000}</c>. The generator is pure so the
/// uniqueness/concurrency tests can exercise it without a database.
/// </summary>
public static class StudentCodeGenerator
{
    public const string Prefix = "STU";
    public const int SequenceDigits = 4;
    public const int MaximumSequence = 9999;

    /// <summary>Builds one code. The year is not padded.</summary>
    public static string Build(int year, int sequence)
    {
        if (year is < 1900 or > 9999)
        {
            throw new ArgumentOutOfRangeException(nameof(year), year, "The year must have four digits.");
        }

        if (sequence is < 1 or > MaximumSequence)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequence),
                sequence,
                $"The sequence must be between 1 and {MaximumSequence}.");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Prefix}-{year:D4}-{sequence.ToString($"D{SequenceDigits}", CultureInfo.InvariantCulture)}");
    }

    /// <summary>
    /// Reads the numeric suffix of a code for the supplied year. Codes for
    /// another year, malformed codes and nulls return 0.
    /// </summary>
    public static int ParseSequence(string? code, int year)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return 0;
        }

        var expectedPrefix = $"{Prefix}-{year:D4}-";
        var trimmed = code.Trim();
        if (!trimmed.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var suffix = trimmed[expectedPrefix.Length..];
        return int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var sequence)
            && sequence is >= 1 and <= MaximumSequence
            ? sequence
            : 0;
    }

    /// <summary>
    /// Next free sequence for the year. Existing codes are filtered by prefix,
    /// so a code from another year never forces a gap in this one.
    /// </summary>
    public static int NextSequence(IEnumerable<string?> existingCodes, int year)
    {
        ArgumentNullException.ThrowIfNull(existingCodes);

        var maximum = 0;
        foreach (var code in existingCodes)
        {
            var sequence = ParseSequence(code, year);
            if (sequence > maximum)
            {
                maximum = sequence;
            }
        }

        if (maximum >= MaximumSequence)
        {
            throw new InvalidOperationException(
                $"No student code is available for {year}; the sequence reached {MaximumSequence}.");
        }

        return maximum + 1;
    }

    public static bool IsCodeForYear(string? code, int year)
        => ParseSequence(code, year) > 0;
}
