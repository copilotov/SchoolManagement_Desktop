using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities;

/// <summary>Maps to the original <c>subjects</c> table.</summary>
public sealed class Subject : IAuditableEntity
{
    public int Id { get; set; }

    public int SchoolId { get; set; } = 1;

    public string Name { get; set; } = string.Empty;

    public string? Code { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
