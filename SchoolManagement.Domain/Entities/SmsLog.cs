using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities;

/// <summary>
/// Maps to the original <c>sms_logs</c> table. The app never sends SMS through
/// a gateway; it opens the phone/Phone Link and records the attempt here.
/// </summary>
public sealed class SmsLog : IEntity
{
    public int Id { get; set; }

    public int? NotificationId { get; set; }

    public string Phone { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string Status { get; set; } = SmsStatuses.Sent;

    public string? ErrorMessage { get; set; }

    public int? RecipientId { get; set; }

    public string? RecipientName { get; set; }

    public string? EventCode { get; set; }

    public DateTime SentAt { get; set; }
}
