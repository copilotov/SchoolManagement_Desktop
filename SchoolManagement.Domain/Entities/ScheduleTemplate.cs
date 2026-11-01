using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities;

/// <summary>
/// Maps to the original <c>schedule_templates</c> table. <c>day_of_week</c>
/// uses the school week: 0 = Saturday ... 6 = Friday (see
/// <see cref="WeekDay"/>), which is what the original dialog and entity use.
/// </summary>
public sealed class ScheduleTemplate : IAuditableEntity
{
    public int Id { get; set; }

    public int GroupId { get; set; }

    public int DayOfWeek { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string? Location { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
