using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities;

/// <summary>Maps to the original <c>discounts</c> table.</summary>
public sealed class Discount : IAuditableEntity
{
    public int Id { get; set; }

    public int SchoolId { get; set; } = 1;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public double Percentage { get; set; }

    public string? Conditions { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
