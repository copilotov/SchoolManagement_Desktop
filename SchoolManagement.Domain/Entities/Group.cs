using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Policies;

namespace SchoolManagement.Domain.Entities;

/// <summary>
/// Maps to the original <c>groups</c> table. The phone schema deliberately has
/// no foreign keys for subject/teacher/level/grade here; ids are plain ints.
/// </summary>
public sealed class Group : IAuditableEntity
{
    public int Id { get; set; }

    public int? SubjectId { get; set; }

    public int? TeacherId { get; set; }

    public int? AcademicLevelId { get; set; }

    public int? GradeId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int MaxStudents { get; set; } = GroupCapacityPolicy.DefaultMaxStudents;

    public double MonthlyFee { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
