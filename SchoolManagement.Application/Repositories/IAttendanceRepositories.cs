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

    Task<Result<bool>> SetStatusAsync(
        int sessionId,
        string status,
        string? cancelReason,
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
}

public interface IScheduleTemplateRepository : IGenericRepository<ScheduleTemplate>
{
    Task<Result<IReadOnlyList<ScheduleTemplate>>> GetByGroupAsync(
        int groupId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ScheduleTemplate>>> GetActiveByWeekDayAsync(
        WeekDay day,
        CancellationToken cancellationToken = default);
}
