using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities;

/// <summary>Maps to the original <c>sessions</c> table.</summary>
public sealed class Session : IAuditableEntity
{
    public int Id { get; set; }

    public int GroupId { get; set; }

    public int? TemplateId { get; set; }

    public DateTime SessionDate { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string Type { get; set; } = SessionTypes.Regular;

    public string Status { get; set; } = SessionStatuses.Scheduled;

    public string? LessonTopic { get; set; }

    public string? TeacherNotes { get; set; }

    public string? Homework { get; set; }

    public string? CancelReason { get; set; }

    public bool IsPaid { get; set; }

    public double? Price { get; set; }

    public string? PaymentStatus { get; set; }

    public double PaidAmount { get; set; }

    public double RemainingAmount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
