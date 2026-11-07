using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Time;

namespace SchoolManagement.Application.Attendance;

/// <summary>Filtered session table used by both attendance tabs.</summary>
public sealed class GetSessionListUseCase
{
    private readonly ISessionRepository _sessions;

    public GetSessionListUseCase(ISessionRepository sessions)
    {
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
    }

    public Task<Result<IReadOnlyList<SessionListItem>>> ExecuteAsync(
        SessionListQuery query,
        CancellationToken cancellationToken = default)
        => _sessions.GetListAsync(query, cancellationToken);
}

public sealed class GetSessionSummaryUseCase
{
    private readonly ISessionRepository _sessions;

    public GetSessionSummaryUseCase(ISessionRepository sessions)
    {
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
    }

    public Task<Result<SessionListItem?>> ExecuteAsync(
        int sessionId,
        CancellationToken cancellationToken = default)
        => _sessions.GetSummaryAsync(sessionId, cancellationToken);
}

/// <summary>
/// One read fills the take-attendance screen: the session header, the roster
/// (active enrollments, unmarked students default to present) and the homeworks
/// that target this session.
/// </summary>
public sealed class GetSessionAttendanceBoardUseCase
{
    private readonly ISessionRepository _sessions;
    private readonly IAttendanceRepository _attendance;
    private readonly IHomeworkRepository _homeworks;

    public GetSessionAttendanceBoardUseCase(
        ISessionRepository sessions,
        IAttendanceRepository attendance,
        IHomeworkRepository homeworks)
    {
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _attendance = attendance ?? throw new ArgumentNullException(nameof(attendance));
        _homeworks = homeworks ?? throw new ArgumentNullException(nameof(homeworks));
    }

    public async Task<Result<SessionAttendanceBoard>> ExecuteAsync(
        int sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessions.GetSummaryAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (session.IsFailure)
        {
            return Result<SessionAttendanceBoard>.Fail(session.Failure!);
        }

        if (session.Value is null)
        {
            return Result<SessionAttendanceBoard>.Fail(
                Failure.NotFound(TranslationKeys.Attendance.SessionNotFound));
        }

        var roster = await _attendance.GetRosterAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (roster.IsFailure)
        {
            return Result<SessionAttendanceBoard>.Fail(roster.Failure!);
        }

        var due = await _homeworks.GetDueForSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (due.IsFailure)
        {
            return Result<SessionAttendanceBoard>.Fail(due.Failure!);
        }

        return Result<SessionAttendanceBoard>.Success(
            new SessionAttendanceBoard(session.Value, roster.Value, due.Value));
    }
}

/// <summary>
/// Builds the immutable attendance report bundle (school name, period, filter
/// labels and per-student counters) and leaves PDF/Excel rendering to the port.
/// Read-only: no license gate.
/// </summary>
public sealed class GetAttendanceReportUseCase
{
    private readonly IAttendanceRepository _attendance;
    private readonly IReferenceDataRepository _references;
    private readonly IGroupRepository _groups;
    private readonly ITrustedClock _clock;

    public GetAttendanceReportUseCase(
        IAttendanceRepository attendance,
        IReferenceDataRepository references,
        IGroupRepository groups,
        ITrustedClock clock)
    {
        _attendance = attendance ?? throw new ArgumentNullException(nameof(attendance));
        _references = references ?? throw new ArgumentNullException(nameof(references));
        _groups = groups ?? throw new ArgumentNullException(nameof(groups));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<AttendanceReportData>> ExecuteAsync(
        AttendanceReportQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.ToDate < query.FromDate)
        {
            return Result<AttendanceReportData>.Fail(
                Failure.Validation(TranslationKeys.Validators.InvalidDate, "toDate"));
        }

        var rows = await _attendance.GetReportAsync(query, cancellationToken).ConfigureAwait(false);
        if (rows.IsFailure)
        {
            return Result<AttendanceReportData>.Fail(rows.Failure!);
        }

        var school = await _references.GetSchoolNameAsync(cancellationToken).ConfigureAwait(false);
        if (school.IsFailure)
        {
            return Result<AttendanceReportData>.Fail(school.Failure!);
        }

        string? groupName = null;
        if (query.GroupId is > 0)
        {
            var group = await _groups.GetSummaryAsync(query.GroupId.Value, cancellationToken).ConfigureAwait(false);
            if (group.IsFailure)
            {
                return Result<AttendanceReportData>.Fail(group.Failure!);
            }

            groupName = group.Value?.Name;
        }

        var studentName = query.StudentId is > 0
            ? rows.Value
                .FirstOrDefault(row => row.StudentId == query.StudentId)
                ?.StudentName
            : null;

        return Result<AttendanceReportData>.Success(
            new AttendanceReportData(
                school.Value,
                query.FromDate,
                query.ToDate,
                query.GroupId,
                groupName,
                query.StudentId,
                studentName,
                rows.Value,
                _clock.LocalNow));
    }
}
