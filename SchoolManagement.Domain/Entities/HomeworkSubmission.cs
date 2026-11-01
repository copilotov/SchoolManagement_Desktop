using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities;

/// <summary>
/// Maps to the original <c>homework_submissions</c> table. The unique key is
/// (homework_id, student_id).
/// </summary>
public sealed class HomeworkSubmission : IAuditableEntity
{
    public int Id { get; set; }

    public int HomeworkId { get; set; }

    public int StudentId { get; set; }

    public string Status { get; set; } = HomeworkStatuses.Pending;

    public DateTime? SubmittedAt { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Homework? Homework { get; set; }
}
