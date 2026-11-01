using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities;

/// <summary>
/// Maps to the original <c>exams</c> table. The exams feature was cancelled by
/// the owner; the table is kept for file compatibility and has no UI.
/// </summary>
public sealed class Exam : IAuditableEntity
{
    public int Id { get; set; }

    public int SchoolId { get; set; } = 1;

    public int GroupId { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime ExamDate { get; set; }

    public DateTime? StartTime { get; set; }

    public int? DurationMinutes { get; set; }

    public string Type { get; set; } = string.Empty;

    public double? TotalMarks { get; set; }

    public bool IsExternal { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
