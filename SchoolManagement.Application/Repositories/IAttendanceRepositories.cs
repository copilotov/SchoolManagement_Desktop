using SchoolManagement.Application.Common;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Repositories;

public interface ISessionRepository : IGenericRepository<Session>
{
    Task<Result<IReadOnlyList<SessionListItem>>> GetByDateAsync(
        DateTime date,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<SessionListItem>>> GetByGroupAsync(
        int groupId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> ExistsAsync(
        int groupId,
        DateTime sessionDate,
        DateTime startTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates one regular session per active template of that school day.
    /// A <paramref name="groupId"/> of null generates for every group. Sessions
    /// already present for the group/date/start are counted, never duplicated;
    /// a static gate plus a transaction serializes concurrent callers.
    /// </summary>
    Task<Result<SchoolManagement.Application.Groups.SessionGenerationResult>> GenerateForDateAsync(
        DateTime date,
        int? groupId = null,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> SetStatusAsync(
        int sessionId,
        string status,
        string? cancelReason,
        CancellationToken cancellationToken = default);

    /// <summary>Filtered session table: date range, group, status and type run in SQL.</summary>
    Task<Result<IReadOnlyList<SessionListItem>>> GetListAsync(
        SchoolManagement.Application.Attendance.SessionListQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<SessionListItem?>> GetSummaryAsync(
        int sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates one makeup/extra session. The (group, date, start) key is checked
    /// inside a static gate plus a transaction, so concurrent clicks never
    /// create two identical extra sessions.
    /// </summary>
    Task<Result<SchoolManagement.Application.Attendance.ExtraSessionOutcome>> CreateExtraAsync(
        SchoolManagement.Application.Attendance.ExtraSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> UpdatePaymentAsync(
        int sessionId,
        bool isPaid,
        string? paymentStatus,
        double paidAmount,
        double remainingAmount,
        CancellationToken cancellationToken = default);

    /// <summary>Guardian phones of the active students of one group, used by the SMS suggestion.</summary>
    Task<Result<IReadOnlyList<GuardianContact>>> GetGuardianContactsAsync(
        int groupId,
        CancellationToken cancellationToken = default);
}

public interface IAttendanceRepository : IGenericRepository<AttendanceRecord>
{
    Task<Result<IReadOnlyList<AttendanceListItem>>> GetBySessionAsync(
        int sessionId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AttendanceListItem>>> GetByStudentAsync(
        int studentId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);

    /// <summary>Counts "absent" and "unexcused", the two absence values.</summary>
    Task<Result<int>> CountAbsencesAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>Session and attendance counters for one group, joined in SQL.</summary>
    Task<Result<GroupAttendanceStats>> GetGroupStatisticsAsync(
        int groupId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates one record. The unique (session_id, student_id) key
    /// is enforced by SQLite, and the method never creates duplicates itself.
    /// </summary>
    Task<Result<int>> UpsertAsync(
        int sessionId,
        int studentId,
        string status,
        string? notes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Active enrollments of the session's group left-joined with the attendance
    /// rows. A student without a row arrives with the default "present" status so
    /// the screen can show the whole roster before the first save.
    /// </summary>
    Task<Result<IReadOnlyList<AttendanceRosterItem>>> GetRosterAsync(
        int sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>Bulk upsert of one session in a single transaction.</summary>
    Task<Result<int>> UpsertManyAsync(
        int sessionId,
        IReadOnlyList<SchoolManagement.Application.Attendance.AttendanceEntry> entries,
        CancellationToken cancellationToken = default);

    /// <summary>Per-student counters over a date range, grouped in SQL.</summary>
    Task<Result<IReadOnlyList<AttendanceReportRow>>> GetReportAsync(
        SchoolManagement.Application.Attendance.AttendanceReportQuery query,
        CancellationToken cancellationToken = default);
}

public interface IScheduleTemplateRepository : IGenericRepository<ScheduleTemplate>
{
    Task<Result<IReadOnlyList<ScheduleTemplate>>> GetByGroupAsync(
        int groupId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ScheduleTemplate>>> GetActiveByWeekDayAsync(
        WeekDay day,
        CancellationToken cancellationToken = default);

    /// <summary>Slots joined with group/teacher, optionally filtered for the timetable.</summary>
    Task<Result<IReadOnlyList<ScheduleSlotItem>>> GetSlotsAsync(
        int? groupId,
        int? teacherId,
        bool includeInactive,
        CancellationToken cancellationToken = default);

    Task<Result<ScheduleSlotItem?>> GetSlotAsync(int slotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Slots of the same weekday that overlap in time and belong either to the
    /// same group or to the same teacher. Detection is a warning; the caller
    /// confirms and then saves.
    /// </summary>
    Task<Result<IReadOnlyList<ScheduleSlotItem>>> GetConflictsAsync(
        int groupId,
        int? teacherId,
        int dayOfWeek,
        TimeSpan startTime,
        TimeSpan endTime,
        int? excludeSlotId = null,
        CancellationToken cancellationToken = default);

    Task<Result<SchoolManagement.Application.Groups.ScheduleSaveOutcome>> SaveSlotAsync(
        SchoolManagement.Application.Groups.ScheduleSaveRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> DeleteSlotAsync(int slotId, CancellationToken cancellationToken = default);
}
