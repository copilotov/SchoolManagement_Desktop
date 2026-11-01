using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities;

/// <summary>Maps to the original <c>teachers</c> table.</summary>
public sealed class Teacher : IAuditableEntity
{
    public int Id { get; set; }

    public int SchoolId { get; set; } = 1;

    public string FullName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? PhotoPath { get; set; }

    public string? Specialization { get; set; }

    public DateTime? HireDate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
