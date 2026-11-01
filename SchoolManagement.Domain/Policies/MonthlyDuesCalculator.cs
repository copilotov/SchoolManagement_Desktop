namespace SchoolManagement.Domain.Policies;

/// <summary>
/// Pure monthly dues calculation. Money is a double because the original
/// SQLite schema stores REAL and the phone file must stay readable; no currency
/// rounding is applied here beyond rounding the final outstanding amount.
/// </summary>
public readonly record struct MonthlyDues(
    double Expected,
    double Paid,
    double Exempted,
    double Outstanding);

public static class MonthlyDuesCalculator
{
    /// <summary>
    /// A full exemption covers the whole expected fee. A partial exemption
    /// covers its own amount. The outstanding amount is never negative.
    /// </summary>
    public static MonthlyDues Calculate(
        double monthlyFee,
        double paid,
        double partialExemption,
        bool hasFullExemption)
    {
        if (monthlyFee < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monthlyFee), monthlyFee, "The monthly fee cannot be negative.");
        }

        if (paid < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(paid), paid, "The paid amount cannot be negative.");
        }

        if (partialExemption < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(partialExemption), partialExemption, "The exemption cannot be negative.");
        }

        var exempted = hasFullExemption
            ? monthlyFee
            : Math.Min(partialExemption, monthlyFee);

        var outstanding = Math.Max(0, monthlyFee - paid - exempted);

        return new MonthlyDues(
            Round(monthlyFee),
            Round(paid),
            Round(exempted),
            Round(outstanding));
    }

    /// <summary>Month key used by <c>payments.month_year</c> ("YYYY-MM").</summary>
    public static string ToMonthYear(DateTime date) => $"{date.Year:D4}-{date.Month:D2}";

    private static double Round(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
