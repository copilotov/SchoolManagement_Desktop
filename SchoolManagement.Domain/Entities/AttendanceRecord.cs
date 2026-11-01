using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities;

/// <summary>
/// Maps to the original <c>attendance</c> table. The unique key is
/// (session_id, student_id); <c>status</c> is read tolerantly because builds
/// may also write "unexcused".
/// </summary>
public sealed class AttendanceRecord : IAuditableEntity
{
    public int Id { get; set; }

    public int SessionId { get; set; }

    public int StudentId { get; set; }

    public string Status { get; set; } = AttendanceStatuses.Present;

    public DateTime? CheckInTime { get; set; }

    public int? RecordedBy { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
