using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities;

/// <summary>
/// Maps to the original <c>exam_results</c> table. The unique key is
/// (exam_id, student_id). The exams feature has no UI in this phase.
/// </summary>
public sealed class ExamResult : IAuditableEntity
{
    public int Id { get; set; }

    public int SchoolId { get; set; } = 1;

    public int ExamId { get; set; }

    public int StudentId { get; set; }

    public double? Score { get; set; }

    public string? Notes { get; set; }

    public int? RecordedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Exam? Exam { get; set; }
}
