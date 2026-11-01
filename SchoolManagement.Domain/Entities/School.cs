using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities;

/// <summary>Maps to the original <c>schools</c> table (one row, id = 1).</summary>
public sealed class School : IAuditableEntity
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? LogoPath { get; set; }

    public string DefaultLanguage { get; set; } = "ar";

    public string Timezone { get; set; } = "Africa/Algiers";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
