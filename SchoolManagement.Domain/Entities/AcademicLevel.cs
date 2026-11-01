using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities;

/// <summary>Maps to the original <c>academic_levels</c> table.</summary>
public sealed class AcademicLevel : IAuditableEntity
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int OrderIndex { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
