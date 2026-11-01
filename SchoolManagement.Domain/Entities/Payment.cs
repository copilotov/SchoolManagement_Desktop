using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities;

/// <summary>
/// Maps to the original <c>payments</c> table. A partial payment keeps the
/// original amount and the remaining amount; extra fees link back through
/// <c>parent_payment_id</c>.
/// </summary>
public sealed class Payment : IAuditableEntity
{
    public int Id { get; set; }

    public int StudentId { get; set; }

    public int? GroupId { get; set; }

    public int? SessionId { get; set; }

    public string Type { get; set; } = PaymentTypes.Monthly;

    /// <summary>"YYYY-MM" for monthly/advance payments.</summary>
    public string? MonthYear { get; set; }

    public double Amount { get; set; }

    public double OriginalAmount { get; set; }

    public double RemainingAmount { get; set; }

    public DateTime PaymentDate { get; set; }

    public string Method { get; set; } = PaymentMethods.Cash;

    public string ReceiptNumber { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public int? RecordedBy { get; set; }

    public int? ParentPaymentId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
