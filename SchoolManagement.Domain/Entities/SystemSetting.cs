using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities;

/// <summary>
/// Maps to the original <c>system_settings</c> key/value table. The key is
/// unique; <c>absence_limit</c> defaults to 5 when the row is missing.
/// </summary>
public sealed class SystemSetting : IUpdatedEntity
{
    public int Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; }
}
