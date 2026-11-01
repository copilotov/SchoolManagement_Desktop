namespace SchoolManagement.Application.Repositories.Models;

// Projection models returned by repositories. They contain only the columns a
// screen needs, so a repository never loads a whole table into memory.

public sealed record StudentListItem(
    int Id,
    string StudentCode,
    string FirstName,
    string LastName,
    string? Phone,
    string Status,
    string? GuardianName,
    int? GuardianId);

public sealed record StudentDetails(
    int Id,
    string StudentCode,
    string FirstName,
    string LastName,
    DateTime? DateOfBirth,
    string? Gender,
    string? Address,
    string? Phone,
    string? PhotoPath,
    string Status,
    string? Notes,
    double RegistrationFee,
    int? GuardianId,
    string? GuardianName,
    string? GuardianPhone);

public sealed record StudentAbsenceSummary(
    int StudentId,
    int AbsenceCount,
    int Limit,
    bool HasReachedLimit,
    bool IsExceeded)
{
    public int RemainingBeforeLimit => Math.Max(0, Limit - AbsenceCount);
}

public sealed record GroupSummary(
    int Id,
    string Name,
    int? SubjectId,
    string? SubjectName,
    int? TeacherId,
    string? TeacherName,
    int? AcademicLevelId,
    string? AcademicLevelName,
    int? GradeId,
    string? GradeName,
    int MaxStudents,
    double MonthlyFee,
    bool IsActive,
    int ActiveEnrollments)
{
    public bool IsFull => ActiveEnrollments >= MaxStudents;

    public bool IsOverCapacity => ActiveEnrollments > MaxStudents;

    public int RemainingSeats => Math.Max(0, MaxStudents - ActiveEnrollments);
}

public sealed record EnrollmentListItem(
    int Id,
    int StudentId,
    string StudentCode,
    string StudentName,
    int GroupId,
    string GroupName,
    DateTime EnrollmentDate,
    string Status,
    double RegistrationFee);

public sealed record SessionListItem(
    int Id,
    int GroupId,
    string GroupName,
    DateTime SessionDate,
    DateTime StartTime,
    DateTime EndTime,
    string Type,
    string Status,
    string? LessonTopic,
    bool IsPaid,
    double? Price,
    string? PaymentStatus,
    double PaidAmount,
    double RemainingAmount);

public sealed record AttendanceListItem(
    int Id,
    int SessionId,
    int StudentId,
    string StudentCode,
    string StudentName,
    string Status,
    DateTime? CheckInTime,
    string? Notes);

public sealed record PaymentListItem(
    int Id,
    int StudentId,
    int? GroupId,
    int? SessionId,
    string Type,
    string? MonthYear,
    double Amount,
    double OriginalAmount,
    double RemainingAmount,
    DateTime PaymentDate,
    string Method,
    string ReceiptNumber,
    string? Notes);

public sealed record MonthlyDue(
    int StudentId,
    string StudentCode,
    string StudentName,
    int GroupId,
    string GroupName,
    string MonthYear,
    double Expected,
    double Paid,
    double Exempted,
    double Outstanding);

public sealed record HomeworkListItem(
    int Id,
    int GroupId,
    int SessionId,
    int? TargetSessionId,
    string Title,
    string Description,
    DateTime DueDate,
    bool IsCompleted,
    int SubmissionCount,
    int SubmittedCount,
    int PendingCount);

public sealed record HomeworkSubmissionItem(
    int Id,
    int HomeworkId,
    int StudentId,
    string StudentCode,
    string StudentName,
    string Status,
    DateTime? SubmittedAt,
    string? Notes);

public sealed record NotificationListItem(
    int Id,
    string Title,
    string Body,
    string Type,
    string RecipientType,
    int? RecipientId,
    string Channel,
    bool IsRead,
    string? Metadata,
    DateTime CreatedAt);

public sealed record LookupItem(int Id, string Name);
