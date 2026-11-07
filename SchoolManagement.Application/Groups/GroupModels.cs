using System.Globalization;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Domain.Policies;

namespace SchoolManagement.Application.Groups;

/// <summary>Column a group list may be sorted by.</summary>
public enum GroupSortField
{
    Name = 0,
    Subject = 1,
    Teacher = 2,
    Grade = 3,
    Capacity = 4,
    Fee = 5,
}

/// <summary>
/// Deliberate group query. Every filter and sort runs in SQL and the projection
/// only carries the columns the group table displays.
/// </summary>
public sealed record GroupListQuery(
    string? SearchTerm = null,
    bool IncludeInactive = false,
    GroupSortField SortBy = GroupSortField.Name,
    bool Descending = false);

/// <summary>All fields of the shared group form.</summary>
public sealed record GroupSaveRequest(
    string Name,
    int? AcademicLevelId,
    int? GradeId,
    int? SubjectId,
    int? TeacherId,
    int MaxStudents,
    double MonthlyFee,
    bool IsActive,
    int? GroupId = null);

public sealed record GroupSaveOutcome(int GroupId, bool Created);

/// <summary>Dependency counts used to block a destructive group delete.</summary>
public sealed record GroupDependencies(int Enrollments, int Sessions, int ScheduleSlots)
{
    public bool HasLinks => Enrollments > 0 || Sessions > 0 || ScheduleSlots > 0;
}

/// <summary>One subject linked to a grade through <c>grade_subjects</c>.</summary>
public sealed record GroupSubjectOption(int Id, string Name, string? Code);

/// <summary>The weekly slot as submitted by the schedule dialog.</summary>
public sealed record ScheduleSaveRequest(
    int GroupId,
    int DayOfWeek,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string? Location,
    bool IsActive = true,
    int? SlotId = null);

public sealed record ScheduleSaveOutcome(int SlotId, bool Created);

/// <summary>One slot that prevents a silent save; the UI asks for confirmation.</summary>
public sealed record ScheduleConflictItem(
    ScheduleConflictKind Kind,
    string? GroupName,
    int DayOfWeek,
    TimeSpan Start,
    TimeSpan End);

public sealed record ScheduleCheckResult(
    IReadOnlyList<ScheduleConflictItem> Conflicts)
{
    public bool HasConflicts => Conflicts.Count > 0;
}

/// <summary>Enrolls an existing student in a group; the request never creates students.</summary>
public sealed record EnrollmentSaveRequest(int GroupId, int StudentId, string? Notes = null);

public sealed record EnrollmentSaveOutcome(int EnrollmentId, bool Created, bool AlreadyEnrolled);

/// <summary>Result of <c>GenerateSessionsForDate</c>: no duplicate session is ever created.</summary>
public sealed record SessionGenerationResult(int CreatedCount, int AlreadyExisted)
{
    public int Total => CreatedCount + AlreadyExisted;
}

/// <summary>The date (and optional group) of a session generation run.</summary>
public sealed record SessionGenerationRequest(DateTime Date, int? GroupId = null);

/// <summary>Lookup bundle for the group form and the filters.</summary>
public sealed record GroupLookups(
    IReadOnlyList<LookupItem> Levels,
    IReadOnlyList<LookupItem> Grades,
    IReadOnlyList<LookupItem> Subjects,
    IReadOnlyList<LookupItem> Teachers);

/// <summary>Everything the enrollment screen shows, loaded by one use case.</summary>
public sealed record EnrollmentBoardData(
    GroupSummary Group,
    IReadOnlyList<EnrollmentListItem> Enrolled,
    IReadOnlyList<StudentListItem> Available,
    int ActiveCount);

/// <summary>Everything the group file shows, loaded by one use case.</summary>
public sealed record GroupDetailsData(
    Repositories.Models.GroupSummary Group,
    IReadOnlyList<Repositories.Models.EnrollmentListItem> Students,
    IReadOnlyList<Repositories.Models.ScheduleSlotItem> Slots,
    Repositories.Models.GroupAttendanceStats Attendance);

/// <summary>A student row of the printable group report.</summary>
public sealed record GroupReportStudent(
    int StudentId,
    string StudentCode,
    string FullName,
    string Status,
    string? GuardianPhone,
    double Outstanding);

/// <summary>Everything the QuestPDF group report needs, already joined.</summary>
public sealed record GroupReportData(
    string SchoolName,
    string? AcademicYear,
    Repositories.Models.GroupSummary Group,
    IReadOnlyList<GroupReportStudent> Students,
    Repositories.Models.GroupAttendanceStats Attendance,
    IReadOnlyList<Repositories.Models.MonthlyDue> Dues,
    string MonthYear,
    DateTime GeneratedAt)
{
    public double TotalOutstanding => Dues.Sum(due => due.Outstanding);

    public double TotalPaid => Dues.Sum(due => due.Paid);

    public double TotalExpected => Dues.Sum(due => due.Expected);
}

/// <summary>Pure validation shared by the group form and the save use case.</summary>
public static class GroupValidator
{
    public const int NameMaxLength = 100;
    public const int LocationMaxLength = 100;
    public const int MaxStudentsLimit = 500;

    public static Failure? Validate(GroupSaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return Failure.Validation(TranslationKeys.Validators.Required, "name");
        }

        if (name.Length > NameMaxLength)
        {
            return Failure.Validation(
                TranslationKeys.Validators.MaxLength,
                "name",
                parameters: MaxLengthParameters(NameMaxLength));
        }

        if (request.GradeId is null or <= 0)
        {
            return Failure.Validation(TranslationKeys.Validators.Required, "gradeId");
        }

        if (request.SubjectId is null or <= 0)
        {
            return Failure.Validation(TranslationKeys.Validators.Required, "subjectId");
        }

        if (request.MaxStudents <= 0 || request.MaxStudents > MaxStudentsLimit)
        {
            return Failure.Validation(TranslationKeys.Validators.PositiveNumber, "maxStudents");
        }

        if (request.MonthlyFee < 0)
        {
            return Failure.Validation(TranslationKeys.Validators.PositiveNumber, "monthlyFee");
        }

        return null;
    }

    public static GroupSaveRequest Normalize(GroupSaveRequest request) => request with
    {
        Name = request.Name.Trim(),
        AcademicLevelId = request.AcademicLevelId is > 0 ? request.AcademicLevelId : null,
        GradeId = request.GradeId is > 0 ? request.GradeId : null,
        SubjectId = request.SubjectId is > 0 ? request.SubjectId : null,
        TeacherId = request.TeacherId is > 0 ? request.TeacherId : null,
    };

    public static IReadOnlyDictionary<string, string> MaxLengthParameters(int count)
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["count"] = count.ToString(CultureInfo.InvariantCulture),
        };
}

/// <summary>Pure validation of a weekly slot.</summary>
public static class ScheduleValidator
{
    public static Failure? Validate(ScheduleSaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.GroupId <= 0)
        {
            return Failure.Validation(TranslationKeys.Validators.Required, "groupId");
        }

        if (!ScheduleConflictPolicy.IsValidDay(request.DayOfWeek))
        {
            return Failure.Validation(TranslationKeys.Validators.InvalidField, "dayOfWeek");
        }

        if (!ScheduleConflictPolicy.IsValidRange(request.StartTime, request.EndTime))
        {
            return Failure.Validation(TranslationKeys.Groups.InvalidTimeRange, "endTime");
        }

        if (!string.IsNullOrWhiteSpace(request.Location)
            && request.Location.Trim().Length > GroupValidator.LocationMaxLength)
        {
            return Failure.Validation(
                TranslationKeys.Validators.MaxLength,
                "location",
                parameters: GroupValidator.MaxLengthParameters(GroupValidator.LocationMaxLength));
        }

        return null;
    }

    public static ScheduleSaveRequest Normalize(ScheduleSaveRequest request) => request with
    {
        Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim(),
    };
}

/// <summary>Parses the "HH:mm" text used by the schedule dialog.</summary>
public static class ScheduleTimeParser
{
    public static bool TryParse(string? text, out TimeSpan value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return TimeSpan.TryParseExact(
            text.Trim(),
            new[] { @"hh\:mm", @"h\:mm", @"hh\:mm\:ss" },
            CultureInfo.InvariantCulture,
            out value);
    }

    public static string Format(TimeSpan value) => value.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

    public static string Format(DateTime value) => value.ToString("HH:mm", CultureInfo.InvariantCulture);
}
