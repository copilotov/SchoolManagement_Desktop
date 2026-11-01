using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities;

/// <summary>Maps to the original <c>homeworks</c> table.</summary>
public sealed class Homework : IAuditableEntity
{
    public int Id { get; set; }

    public int SessionId { get; set; }

    public int? TargetSessionId { get; set; }

    public int GroupId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime DueDate { get; set; }

    public bool IsCompleted { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
