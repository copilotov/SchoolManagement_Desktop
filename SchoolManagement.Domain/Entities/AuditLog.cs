using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities;

/// <summary>Maps to the original <c>audit_logs</c> table.</summary>
public sealed class AuditLog : ICreatedEntity
{
    public int Id { get; set; }

    public int SchoolId { get; set; } = 1;

    public int? UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public int? EntityId { get; set; }

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public DateTime CreatedAt { get; set; }
}
