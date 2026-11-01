using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities;

/// <summary>Maps to the original <c>notifications</c> table (in-app only).</summary>
public sealed class Notification : ICreatedEntity
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string Type { get; set; } = NotificationTypes.General;

    public string RecipientType { get; set; } = RecipientTypes.All;

    public int? RecipientId { get; set; }

    public string Channel { get; set; } = NotificationChannels.InApp;

    public bool IsRead { get; set; }

    /// <summary>JSON payload used by the phone app for extra data.</summary>
    public string? Metadata { get; set; }

    public DateTime CreatedAt { get; set; }
}
