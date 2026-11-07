using SchoolManagement.Application.Common;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Services;
using SchoolManagement.Application.Time;
using SchoolManagement.Application.UseCases;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Policies;

namespace SchoolManagement.Application.Attendance;

/// <summary>
/// Cancels, postpones, restores or starts one session. The transition itself is
/// decided by <see cref="SessionStatusPolicy"/> so the rule is testable without
/// a database; the use case only loads the stored status, applies the policy and
/// writes the row.
/// </summary>
[WriteOperation]
public sealed class ChangeSessionStatusUseCase
    : WriteUseCase<SessionStatusChangeRequest, SessionStatusChangeResult>
{
    private readonly ISessionRepository _sessions;

    public ChangeSessionStatusUseCase(ISessionRepository sessions, ILicenseGuard guard)
        : base(guard)
    {
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
    }

    protected override async Task<Result<SessionStatusChangeResult>> ExecuteCoreAsync(
        SessionStatusChangeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = AttendanceValidator.ValidateStatusChange(request);
        if (validation is not null)
        {
            return Result<SessionStatusChangeResult>.Fail(validation);
        }

        var session = await _sessions
            .GetSummaryAsync(request.SessionId, cancellationToken)
            .ConfigureAwait(false);

        if (session.IsFailure)
        {
            return Result<SessionStatusChangeResult>.Fail(session.Failure!);
        }

        if (session.Value is null)
        {
            return Result<SessionStatusChangeResult>.Fail(
                Failure.NotFound(TranslationKeys.Attendance.SessionNotFound));
        }

        var transition = SessionStatusPolicy.Evaluate(session.Value.Status, request.Status);
        if (!transition.Allowed)
        {
            return Result<SessionStatusChangeResult>.Fail(
                Failure.Validation(TranslationKeys.Validators.InvalidField, "status"));
        }

        var reason = string.IsNullOrWhiteSpace(request.CancelReason)
            ? null
            : request.CancelReason.Trim();

        var saved = await _sessions
            .SetStatusAsync(request.SessionId, request.Status, reason, cancellationToken)
            .ConfigureAwait(false);

        if (saved.IsFailure)
        {
            return Result<SessionStatusChangeResult>.Fail(saved.Failure!);
        }

        return Result<SessionStatusChangeResult>.Success(
            new SessionStatusChangeResult(request.Status, transition.SuggestsSms));
    }
}

/// <summary>
/// Creates one makeup/extra session from the attendance screen. The repository
/// serializes the (group, date, start) check so a double click reports the
/// existing row instead of creating a duplicate.
/// </summary>
[WriteOperation]
public sealed class CreateExtraSessionUseCase
    : WriteUseCase<ExtraSessionRequest, ExtraSessionOutcome>
{
    private readonly ISessionRepository _sessions;

    public CreateExtraSessionUseCase(ISessionRepository sessions, ILicenseGuard guard)
        : base(guard)
    {
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
    }

    protected override Task<Result<ExtraSessionOutcome>> ExecuteCoreAsync(
        ExtraSessionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = AttendanceValidator.ValidateExtraSession(request);
        if (validation is not null)
        {
            return Task.FromResult(Result<ExtraSessionOutcome>.Fail(validation));
        }

        return _sessions.CreateExtraAsync(request, cancellationToken);
    }
}

/// <summary>
/// Bulk save of one session. Records are written first, the session becomes
/// completed, then the absence limit is evaluated.
///
/// The notification is a best-effort side effect of an already-guarded write:
/// <see cref="AutoNotificationService"/> writes through the notification
/// repository directly instead of wrapping a second guarded use case, because
/// the license gate has already run exactly once at this point.
/// </summary>
[WriteOperation]
public sealed class SaveSessionAttendanceUseCase
    : WriteUseCase<SaveSessionAttendanceRequest, SessionAttendanceOutcome>
{
    private readonly ISessionRepository _sessions;
    private readonly IAttendanceRepository _attendance;
    private readonly AutoNotificationService _notifications;

    public SaveSessionAttendanceUseCase(
        ISessionRepository sessions,
        IAttendanceRepository attendance,
        AutoNotificationService notifications,
        ILicenseGuard guard)
        : base(guard)
    {
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _attendance = attendance ?? throw new ArgumentNullException(nameof(attendance));
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
    }

    protected override async Task<Result<SessionAttendanceOutcome>> ExecuteCoreAsync(
        SaveSessionAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = AttendanceValidator.ValidateBulkSave(request);
        if (validation is not null)
        {
            return Result<SessionAttendanceOutcome>.Fail(validation);
        }

        var session = await _sessions
            .GetSummaryAsync(request.SessionId, cancellationToken)
            .ConfigureAwait(false);

        if (session.IsFailure)
        {
            return Result<SessionAttendanceOutcome>.Fail(session.Failure!);
        }

        if (session.Value is null)
        {
            return Result<SessionAttendanceOutcome>.Fail(
                Failure.NotFound(TranslationKeys.Attendance.SessionNotFound));
        }

        if (!SessionStatusPolicy.CanTakeAttendance(session.Value.Status))
        {
            return Result<SessionAttendanceOutcome>.Fail(
                Failure.Validation(TranslationKeys.Attendance.SessionClosed, "status"));
        }

        var saved = await _attendance
            .UpsertManyAsync(request.SessionId, request.Entries, cancellationToken)
            .ConfigureAwait(false);

        if (saved.IsFailure)
        {
            return Result<SessionAttendanceOutcome>.Fail(saved.Failure!);
        }

        if (request.MarkCompleted)
        {
            var completed = await _sessions
                .SetStatusAsync(
                    request.SessionId,
                    SessionStatuses.Completed,
                    null,
                    cancellationToken)
                .ConfigureAwait(false);

            if (completed.IsFailure)
            {
                return Result<SessionAttendanceOutcome>.Fail(completed.Failure!);
            }
        }

        var absentStudentIds = request.Entries
            .Where(entry => AttendanceStatuses.IsAbsence(entry.Status))
            .Select(entry => entry.StudentId)
            .Distinct()
            .ToList();

        // A notification failure never turns a saved attendance into a failed
        // save: the roster is already written and the counters stay meaningful.
        var notifications = await _notifications
            .CheckAttendanceLimitAsync(absentStudentIds, cancellationToken)
            .ConfigureAwait(false);

        return Result<SessionAttendanceOutcome>.Success(
            new SessionAttendanceOutcome(
                request.SessionId,
                saved.Value,
                absentStudentIds.Count,
                notifications.IsSuccess ? notifications.Value : 0));
    }
}

/// <summary>
/// Pays or exempts an extra session. "paid" writes a group-level payment row
/// (<c>StudentId = 0</c>, type <c>extra</c>) and updates the session counters;
/// "exempted" only writes the exemption status.
/// </summary>
[WriteOperation]
public sealed class SetSessionPaymentUseCase
    : WriteUseCase<SessionPaymentRequest, SessionPaymentOutcome>
{
    private readonly ISessionRepository _sessions;
    private readonly IPaymentRepository _payments;
    private readonly ITrustedClock _clock;

    public SetSessionPaymentUseCase(
        ISessionRepository sessions,
        IPaymentRepository payments,
        ITrustedClock clock,
        ILicenseGuard guard)
        : base(guard)
    {
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _payments = payments ?? throw new ArgumentNullException(nameof(payments));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    protected override async Task<Result<SessionPaymentOutcome>> ExecuteCoreAsync(
        SessionPaymentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = AttendanceValidator.ValidatePayment(request);
        if (validation is not null)
        {
            return Result<SessionPaymentOutcome>.Fail(validation);
        }

        var session = await _sessions
            .GetSummaryAsync(request.SessionId, cancellationToken)
            .ConfigureAwait(false);

        if (session.IsFailure)
        {
            return Result<SessionPaymentOutcome>.Fail(session.Failure!);
        }

        if (session.Value is null)
        {
            return Result<SessionPaymentOutcome>.Fail(
                Failure.NotFound(TranslationKeys.Attendance.SessionNotFound));
        }

        if (string.Equals(request.Mode, SessionPaymentModes.Exempted, StringComparison.OrdinalIgnoreCase))
        {
            var exempted = await _sessions
                .UpdatePaymentAsync(
                    request.SessionId,
                    false,
                    PaymentStatuses.Exempted,
                    0,
                    0,
                    cancellationToken)
                .ConfigureAwait(false);

            if (exempted.IsFailure)
            {
                return Result<SessionPaymentOutcome>.Fail(exempted.Failure!);
            }

            return Result<SessionPaymentOutcome>.Success(
                new SessionPaymentOutcome(request.SessionId, null, PaymentStatuses.Exempted));
        }

        var price = session.Value.Price ?? 0;
        var amount = request.Amount > 0 ? request.Amount : price;
        if (amount <= 0)
        {
            return Result<SessionPaymentOutcome>.Fail(
                Failure.Validation(TranslationKeys.Validators.PositiveNumber, "amount"));
        }

        var now = _clock.LocalNow;
        var payment = new Payment
        {
            StudentId = 0,
            GroupId = session.Value.GroupId,
            SessionId = session.Value.Id,
            Type = PaymentTypes.Extra,
            MonthYear = null,
            Amount = amount,
            OriginalAmount = amount,
            RemainingAmount = 0,
            PaymentDate = DateTime.SpecifyKind(now, DateTimeKind.Utc),
            Method = string.IsNullOrWhiteSpace(request.Method) ? PaymentMethods.Cash : request.Method,
            ReceiptNumber = "SES-" + request.SessionId.ToString(
                System.Globalization.CultureInfo.InvariantCulture)
                + "-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };

        var saved = await _payments.AddAsync(payment, cancellationToken).ConfigureAwait(false);
        if (saved.IsFailure)
        {
            return Result<SessionPaymentOutcome>.Fail(saved.Failure!);
        }

        var paidTotal = session.Value.PaidAmount + amount;
        var remaining = Math.Max(0, price - paidTotal);
        var status = remaining <= 0 ? PaymentStatuses.Paid : PaymentStatuses.Partial;

        var updated = await _sessions
            .UpdatePaymentAsync(
                request.SessionId,
                remaining <= 0,
                status,
                paidTotal,
                remaining,
                cancellationToken)
            .ConfigureAwait(false);

        if (updated.IsFailure)
        {
            return Result<SessionPaymentOutcome>.Fail(updated.Failure!);
        }

        return Result<SessionPaymentOutcome>.Success(
            new SessionPaymentOutcome(request.SessionId, saved.Value, status));
    }
}

/// <summary>
/// Builds the localized SMS body and records one <c>sms_logs</c> row per
/// guardian phone. The app is offline-only: the desktop opens the messaging
/// application, this use case only persists the attempt.
/// </summary>
[WriteOperation]
public sealed class SendSessionSmsUseCase
    : WriteUseCase<SessionSmsRequest, SessionSmsOutcome>
{
    private readonly ISessionRepository _sessions;
    private readonly ISmsService _sms;
    private readonly ILocalizationService _localization;

    public SendSessionSmsUseCase(
        ISessionRepository sessions,
        ISmsService sms,
        ILocalizationService localization,
        ILicenseGuard guard)
        : base(guard)
    {
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _sms = sms ?? throw new ArgumentNullException(nameof(sms));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
    }

    protected override async Task<Result<SessionSmsOutcome>> ExecuteCoreAsync(
        SessionSmsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var templateKey = ResolveTemplateKey(request.EventCode);
        if (templateKey is null)
        {
            return Result<SessionSmsOutcome>.Fail(
                Failure.Validation(TranslationKeys.Validators.InvalidField, "eventCode"));
        }

        var session = await _sessions
            .GetSummaryAsync(request.SessionId, cancellationToken)
            .ConfigureAwait(false);

        if (session.IsFailure)
        {
            return Result<SessionSmsOutcome>.Fail(session.Failure!);
        }

        if (session.Value is null)
        {
            return Result<SessionSmsOutcome>.Fail(
                Failure.NotFound(TranslationKeys.Attendance.SessionNotFound));
        }

        var contacts = await _sessions
            .GetGuardianContactsAsync(session.Value.GroupId, cancellationToken)
            .ConfigureAwait(false);

        if (contacts.IsFailure)
        {
            return Result<SessionSmsOutcome>.Fail(contacts.Failure!);
        }

        var phones = contacts.Value
            .Where(contact => !string.IsNullOrWhiteSpace(contact.Phone))
            .ToList();

        if (phones.Count == 0)
        {
            return Result<SessionSmsOutcome>.Fail(
                Failure.Validation(TranslationKeys.Notifications.NoPhoneNumber));
        }

        var date = session.Value.SessionDate.ToString(
            "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture);
        var start = session.Value.StartTime.ToString(
            "HH:mm",
            System.Globalization.CultureInfo.InvariantCulture);
        var end = session.Value.EndTime.ToString(
            "HH:mm",
            System.Globalization.CultureInfo.InvariantCulture);

        var body = _localization.Tr(
            templateKey,
            ("groupName", session.Value.GroupName),
            ("date", date),
            ("startTime", start),
            ("endTime", end),
            ("reason", session.Value.CancelReason ?? string.Empty),
            ("oldDate", $"{date} {start}"),
            ("newDate", $"{date} {end}"));

        var logCount = 0;
        foreach (var contact in phones)
        {
            var sent = await _sms.SendAsync(
                    new SmsSendRequest(
                        contact.Phone!,
                        body,
                        request.EventCode,
                        contact.StudentId,
                        contact.StudentName),
                    cancellationToken)
                .ConfigureAwait(false);

            if (sent.IsFailure)
            {
                return Result<SessionSmsOutcome>.Fail(sent.Failure!);
            }

            logCount++;
        }

        return Result<SessionSmsOutcome>.Success(
            new SessionSmsOutcome(
                logCount,
                phones.Select(contact => contact.Phone!).ToList(),
                body));
    }

    private static string? ResolveTemplateKey(string? eventCode) => eventCode?.Trim().ToLowerInvariant() switch
    {
        SessionStatuses.Cancelled => TranslationKeys.SmsTemplates.SessionCancelled,
        SessionStatuses.Postponed => TranslationKeys.SmsTemplates.PostponedSession,
        SessionTypes.Makeup => TranslationKeys.SmsTemplates.MakeupSession,
        SessionTypes.Extra => TranslationKeys.SmsTemplates.MakeupSession,
        "sms" => TranslationKeys.SmsTemplates.SessionCancelled,
        _ => null,
    };
}
