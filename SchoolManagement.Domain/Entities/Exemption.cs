using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities;

/// <summary>
/// Maps to the original <c>exemptions</c> table. A full exemption covers the
/// month/session, a partial one carries the reduced amount.
/// </summary>
public sealed class Exemption : ICreatedEntity
{
    public int Id { get; set; }

    public int StudentId { get; set; }

    public string Type { get; set; } = ExemptionTypes.Partial;

    public double Amount { get; set; }

    public string? MonthYear { get; set; }

    public int? ExtraFeeId { get; set; }

    public string Reason { get; set; } = string.Empty;

    public int? AuthorizedBy { get; set; }

    public DateTime CreatedAt { get; set; }
}
