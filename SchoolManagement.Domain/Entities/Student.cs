using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities;

/// <summary>
/// Maps to the original <c>students</c> table. <c>student_code</c> is unique
/// and is the value encoded in the student QR badge.
/// </summary>
public sealed class Student : IAuditableEntity
{
    public int Id { get; set; }

    public int SchoolId { get; set; } = 1;

    public int? GuardianId { get; set; }

    public string StudentCode { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public DateTime? DateOfBirth { get; set; }

    public string? Gender { get; set; }

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string? PhotoPath { get; set; }

    public string Status { get; set; } = StudentStatuses.Active;

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>Registration fee copied from the enrollment flow (REAL).</summary>
    public double RegistrationFee { get; set; }
}
