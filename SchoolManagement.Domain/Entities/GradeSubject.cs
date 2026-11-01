using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities;

/// <summary>
/// Maps to the original <c>grade_subjects</c> join table. Both foreign keys
/// existed in the phone schema and are kept here.
/// </summary>
public sealed class GradeSubject : ICreatedEntity
{
    public int Id { get; set; }

    public int GradeId { get; set; }

    public int SubjectId { get; set; }

    public DateTime CreatedAt { get; set; }

    public Grade? Grade { get; set; }

    public Subject? Subject { get; set; }
}
