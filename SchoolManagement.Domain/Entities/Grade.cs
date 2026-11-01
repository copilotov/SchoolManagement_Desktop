using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities;

/// <summary>
/// Maps to the original <c>grades</c> table. This is the only table with a
/// foreign key to <c>academic_levels</c>, exactly like the phone schema.
/// </summary>
public sealed class Grade : IAuditableEntity
{
    public int Id { get; set; }

    public int? AcademicLevelId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int OrderIndex { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public AcademicLevel? AcademicLevel { get; set; }
}
