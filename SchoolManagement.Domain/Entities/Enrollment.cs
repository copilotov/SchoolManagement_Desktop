using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities;

/// <summary>
/// Maps to the original <c>enrollments</c> table. <c>group_id</c> was added by
/// schema v2; the initializer upgrades a v1 file by adding that column.
/// </summary>
public sealed class Enrollment : IAuditableEntity
{
    public int Id { get; set; }

    public int SchoolId { get; set; } = 1;

    public int StudentId { get; set; }

    public int GroupId { get; set; }

    public DateTime EnrollmentDate { get; set; }

    public double RegistrationFee { get; set; }

    public string Status { get; set; } = EnrollmentStatuses.Active;

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
