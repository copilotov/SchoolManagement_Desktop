using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities;

/// <summary>
/// Maps to the original <c>invoices</c> table. The phone app has almost
/// abandoned invoices in favour of payments/extra fees/exemptions; the table is
/// kept for file compatibility and is not used by the finance UI.
/// </summary>
public sealed class Invoice : IAuditableEntity
{
    public int Id { get; set; }

    public int SchoolId { get; set; } = 1;

    public string InvoiceNumber { get; set; } = string.Empty;

    public int StudentId { get; set; }

    public int? GroupId { get; set; }

    public int? SessionId { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public double Amount { get; set; }

    public DateTime DueDate { get; set; }

    public string Status { get; set; } = PaymentStatuses.Unpaid;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
