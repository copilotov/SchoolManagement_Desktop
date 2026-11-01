using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities;

/// <summary>
/// Maps to the original <c>fee_settings</c> table. It holds the registration
/// fee and the 3/6/12-month advance discounts used by the finance screen.
/// </summary>
public sealed class FeeSetting : IUpdatedEntity
{
    public int Id { get; set; }

    public bool RegistrationFeeEnabled { get; set; }

    public double RegistrationFeeAmount { get; set; }

    public double Discount3Months { get; set; }

    public double Discount6Months { get; set; }

    public double Discount12Months { get; set; }

    public DateTime UpdatedAt { get; set; }
}
