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
    int? GuardianId,
    string? LevelName = null,
    string? GradeName = null,
    string? GroupNames = null,
    double RegistrationFee = 0);

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
    string? GuardianPhone,
    string? GuardianEmail = null,
    string? GuardianRelationship = null);

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
    int ActiveEnrollments,
    int WeeklySlots = 0)
{
    public bool IsFull => ActiveEnrollments >= MaxStudents;

    public bool IsOverCapacity => ActiveEnrollments > MaxStudents;

    public int RemainingSeats => Math.Max(0, MaxStudents - ActiveEnrollments);

    public bool HasSchedule => WeeklySlots > 0;

    /// <summary>Capacity badge text ("12/30"), never localized on its own.</summary>
    public string CapacityText => $"{ActiveEnrollments}/{MaxStudents}";
}

/// <summary>
/// One weekly slot joined with its group and teacher. The timetable and the
/// schedule page both consume this projection, so neither screen runs a query
/// per row.
/// </summary>
public sealed record ScheduleSlotItem(
    int Id,
    int GroupId,
    string GroupName,
    int? TeacherId,
    string? TeacherName,
    int DayOfWeek,
    DateTime StartTime,
    DateTime EndTime,
    string? Location,
    bool IsActive);

/// <summary>Attendance counters used by the group file and the printable report.</summary>
public sealed record GroupAttendanceStats(
    int SessionCount,
    int RecordCount,
    int PresentCount,
    int AbsentCount,
    int LateCount)
{
    public double AttendanceRate => RecordCount == 0
        ? 0
        : (PresentCount + LateCount) * 100d / RecordCount;
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
    double RemainingAmount,
    string? CancelReason = null);

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

// Phase 4: academic structure. Every list projection is shaped for one screen:
// it carries the linked counts the screen shows, so the DataGrid never N+1
// queries and no repository loads an entire table into memory.

public sealed record AcademicLevelListItem(
    int Id,
    string Name,
    int OrderIndex,
    bool IsActive,
    int GradesCount,
    int GroupsCount);

public sealed record GradeListItem(
    int Id,
    string Name,
    int? AcademicLevelId,
    string? AcademicLevelName,
    int OrderIndex,
    bool IsActive,
    int SubjectsCount,
    int GroupsCount);

public sealed record SubjectListItem(
    int Id,
    string Name,
    string? Code,
    string? Description,
    bool IsActive,
    int GradesCount,
    int GroupsCount);

public sealed record TeacherListItem(
    int Id,
    string FullName,
    string? Phone,
    string? Email,
    string? Address,
    string? Specialization,
    DateTime? HireDate,
    bool IsActive,
    int GroupsCount);

public sealed record SubjectSelectionItem(int Id, string Name, string? Code, bool IsSelected);

// Phase 7: attendance and homework. The roster joins the active enrollments of
// the session's group with the attendance rows, so every student appears once
// even before the first save.

public sealed record AttendanceRosterItem(
    int StudentId,
    string StudentCode,
    string StudentName,
    string Status,
    DateTime? CheckInTime,
    string? Notes);

public sealed record AttendanceReportRow(
    int StudentId,
    string StudentCode,
    string StudentName,
    int GroupId,
    string GroupName,
    int SessionCount,
    int PresentCount,
    int AbsentCount,
    int LateCount)
{
    public int TotalCount => PresentCount + AbsentCount + LateCount;

    public double AttendanceRate => TotalCount == 0
        ? 0
        : (PresentCount + LateCount) * 100d / TotalCount;
}

/// <summary>Guardian phone used by the session SMS suggestion.</summary>
public sealed record GuardianContact(
    int StudentId,
    string StudentName,
    string? GuardianName,
    string? Phone);
