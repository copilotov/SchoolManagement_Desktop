using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities;

/// <summary>Maps to the original <c>academic_years</c> table.</summary>
public sealed class AcademicYear : IAuditableEntity
{
    public int Id { get; set; }

    public int SchoolId { get; set; } = 1;

    public string Name { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public bool IsCurrent { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
