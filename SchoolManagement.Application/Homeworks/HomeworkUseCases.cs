using SchoolManagement.Application.Common;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Time;
using SchoolManagement.Application.UseCases;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Policies;

namespace SchoolManagement.Application.Homeworks;

/// <summary>Builds the homework report bundle (school, group name and rows).</summary>
public sealed class GetHomeworkReportUseCase
{
    private readonly IHomeworkRepository _homeworks;
    private readonly IGroupRepository _groups;
    private readonly IReferenceDataRepository _references;
    private readonly ITrustedClock _clock;

    public GetHomeworkReportUseCase(
        IHomeworkRepository homeworks,
        IGroupRepository groups,
        IReferenceDataRepository references,
        ITrustedClock clock)
    {
        _homeworks = homeworks ?? throw new ArgumentNullException(nameof(homeworks));
        _groups = groups ?? throw new ArgumentNullException(nameof(groups));
        _references = references ?? throw new ArgumentNullException(nameof(references));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<HomeworkReportData>> ExecuteAsync(
        HomeworkReportQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.FromDate is { } from && query.ToDate is { } to && to < from)
        {
            return Result<HomeworkReportData>.Fail(
                Failure.Validation(TranslationKeys.Validators.InvalidDate, "toDate"));
        }

        var rows = await _homeworks.GetListAsync(query, cancellationToken).ConfigureAwait(false);
        if (rows.IsFailure)
        {
            return Result<HomeworkReportData>.Fail(rows.Failure!);
        }

        var school = await _references.GetSchoolNameAsync(cancellationToken).ConfigureAwait(false);
        if (school.IsFailure)
        {
            return Result<HomeworkReportData>.Fail(school.Failure!);
        }

        string? groupName = null;
        if (query.GroupId is > 0)
        {
            var group = await _groups
                .GetSummaryAsync(query.GroupId.Value, cancellationToken)
                .ConfigureAwait(false);

            if (group.IsFailure)
            {
                return Result<HomeworkReportData>.Fail(group.Failure!);
            }

            groupName = group.Value?.Name;
        }

        return Result<HomeworkReportData>.Success(
            new HomeworkReportData(
                school.Value,
                query.GroupId,
                groupName,
                query.FromDate,
                query.ToDate,
                rows.Value,
                _clock.LocalNow));
    }
}

/// <summary>One homework plus the roster of its group, pending students included.</summary>
public sealed class GetHomeworkSubmissionBoardUseCase
{
    private readonly IHomeworkRepository _homeworks;

    public GetHomeworkSubmissionBoardUseCase(IHomeworkRepository homeworks)
    {
        _homeworks = homeworks ?? throw new ArgumentNullException(nameof(homeworks));
    }

    public async Task<Result<HomeworkSubmissionBoard>> ExecuteAsync(
        int homeworkId,
        CancellationToken cancellationToken = default)
    {
        var homework = await _homeworks
            .GetSummaryAsync(homeworkId, cancellationToken)
            .ConfigureAwait(false);

        if (homework.IsFailure)
        {
            return Result<HomeworkSubmissionBoard>.Fail(homework.Failure!);
        }

        if (homework.Value is null)
        {
            return Result<HomeworkSubmissionBoard>.Fail(
                Failure.NotFound(TranslationKeys.Homework.NotFound));
        }

        var board = await _homeworks
            .GetSubmissionBoardAsync(homeworkId, cancellationToken)
            .ConfigureAwait(false);

        if (board.IsFailure)
        {
            return Result<HomeworkSubmissionBoard>.Fail(board.Failure!);
        }

        return Result<HomeworkSubmissionBoard>.Success(
            new HomeworkSubmissionBoard(homework.Value, board.Value));
    }
}

/// <summary>Creates or updates one homework row from the shared add/edit overlay.</summary>
[WriteOperation]
public sealed class SaveHomeworkUseCase : WriteUseCase<HomeworkSaveRequest, HomeworkSaveOutcome>
{
    private readonly IHomeworkRepository _homeworks;
    private readonly ITrustedClock _clock;

    public SaveHomeworkUseCase(
        IHomeworkRepository homeworks,
        ITrustedClock clock,
        ILicenseGuard guard)
        : base(guard)
    {
        _homeworks = homeworks ?? throw new ArgumentNullException(nameof(homeworks));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    protected override async Task<Result<HomeworkSaveOutcome>> ExecuteCoreAsync(
        HomeworkSaveRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = HomeworkValidator.Validate(request);
        if (validation is not null)
        {
            return Result<HomeworkSaveOutcome>.Fail(validation);
        }

        request = HomeworkValidator.Normalize(request);
        var now = _clock.LocalNow;

        if (request.HomeworkId is > 0)
        {
            var existing = await _homeworks
                .GetByIdAsync(request.HomeworkId.Value, cancellationToken)
                .ConfigureAwait(false);

            if (existing.IsFailure)
            {
                return Result<HomeworkSaveOutcome>.Fail(existing.Failure!);
            }

            if (existing.Value is null)
            {
                return Result<HomeworkSaveOutcome>.Fail(
                    Failure.NotFound(TranslationKeys.Homework.NotFound));
            }

            var homework = existing.Value;
            homework.GroupId = request.GroupId;
            homework.SessionId = request.SessionId;
            homework.TargetSessionId = request.TargetSessionId;
            homework.Title = request.Title;
            homework.Description = request.Description;
            homework.DueDate = request.DueDate;
            homework.Notes = request.Notes;
            homework.UpdatedAt = now;

            var updated = await _homeworks.UpdateAsync(homework, cancellationToken).ConfigureAwait(false);
            if (updated.IsFailure)
            {
                return Result<HomeworkSaveOutcome>.Fail(updated.Failure!);
            }

            return Result<HomeworkSaveOutcome>.Success(
                new HomeworkSaveOutcome(homework.Id, false));
        }

        var created = new Homework
        {
            GroupId = request.GroupId,
            SessionId = request.SessionId,
            TargetSessionId = request.TargetSessionId,
            Title = request.Title,
            Description = request.Description,
            DueDate = request.DueDate,
            IsCompleted = false,
            Notes = request.Notes,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var saved = await _homeworks.AddAsync(created, cancellationToken).ConfigureAwait(false);
        if (saved.IsFailure)
        {
            return Result<HomeworkSaveOutcome>.Fail(saved.Failure!);
        }

        return Result<HomeworkSaveOutcome>.Success(new HomeworkSaveOutcome(saved.Value, true));
    }
}

/// <summary>
/// Writes one submission status. A submitted timestamp is always stored, so the
/// pure <see cref="HomeworkPolicy"/> can resolve "late" from the due date.
/// </summary>
[WriteOperation]
public sealed class SetHomeworkSubmissionStatusUseCase
    : WriteUseCase<HomeworkSubmissionRequest, bool>
{
    private readonly IHomeworkRepository _homeworks;
    private readonly ITrustedClock _clock;

    public SetHomeworkSubmissionStatusUseCase(
        IHomeworkRepository homeworks,
        ITrustedClock clock,
        ILicenseGuard guard)
        : base(guard)
    {
        _homeworks = homeworks ?? throw new ArgumentNullException(nameof(homeworks));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    protected override async Task<Result<bool>> ExecuteCoreAsync(
        HomeworkSubmissionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.HomeworkId <= 0 || request.StudentId <= 0)
        {
            return Result<bool>.Fail(
                Failure.Validation(TranslationKeys.Errors.RequiredFields, "homeworkId"));
        }

        if (!IsValidStatus(request.Status))
        {
            return Result<bool>.Fail(
                Failure.Validation(TranslationKeys.Validators.InvalidField, "status"));
        }

        var homework = await _homeworks
            .GetSummaryAsync(request.HomeworkId, cancellationToken)
            .ConfigureAwait(false);

        if (homework.IsFailure)
        {
            return Result<bool>.Fail(homework.Failure!);
        }

        if (homework.Value is null)
        {
            return Result<bool>.Fail(Failure.NotFound(TranslationKeys.Homework.NotFound));
        }

        var today = DateTime.SpecifyKind(_clock.LocalNow.Date, DateTimeKind.Utc);
        DateTime? submittedAt = null;
        string status;

        if (string.Equals(request.Status, HomeworkStatuses.Pending, StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.Status, HomeworkStatuses.Missing, StringComparison.OrdinalIgnoreCase))
        {
            status = request.Status.ToLowerInvariant();
        }
        else
        {
            submittedAt = request.SubmittedAt ?? _clock.LocalNow;
            status = HomeworkPolicy.Resolve(submittedAt, homework.Value.DueDate, today);
        }

        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        return await _homeworks
            .SetSubmissionStatusAsync(
                request.HomeworkId,
                request.StudentId,
                status,
                submittedAt,
                notes,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsValidStatus(string? status)
        => string.Equals(status, HomeworkStatuses.Pending, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, HomeworkStatuses.Submitted, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, HomeworkStatuses.Late, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, HomeworkStatuses.Missing, StringComparison.OrdinalIgnoreCase);
}
