using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities;

/// <summary>Maps to the original <c>extra_fees</c> table.</summary>
public sealed class ExtraFee : IAuditableEntity
{
    public int Id { get; set; }

    public int StudentId { get; set; }

    public string Description { get; set; } = string.Empty;

    public double TotalAmount { get; set; }

    public double PaidAmount { get; set; }

    public double RemainingAmount { get; set; }

    public string Status { get; set; } = ExtraFeeStatuses.Unpaid;

    public DateTime? DueDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
