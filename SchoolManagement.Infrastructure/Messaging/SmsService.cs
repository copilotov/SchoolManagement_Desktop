using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Attendance;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Time;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Messaging;

/// <summary>
/// Records one SMS attempt in <c>sms_logs</c>. The app is offline-only: it never
/// calls a gateway. Opening the messaging application (an <c>sms:</c> URI) is a
/// desktop concern, so this service only persists the log row the phone schema
/// already had.
/// </summary>
public sealed class SmsService : ISmsService
{
    private readonly IDbContextFactory<SchoolManagementDbContext> _contextFactory;
    private readonly ITrustedClock _clock;
    private readonly ILogger<SmsService> _logger;

    public SmsService(
        IDbContextFactory<SchoolManagementDbContext> contextFactory,
        ITrustedClock clock,
        ILogger<SmsService> logger)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<SmsSendResult>> SendAsync(
        SmsSendRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Phone))
        {
            return Result<SmsSendResult>.Fail(
                Failure.Validation(TranslationKeys.Notifications.NoPhoneNumber));
        }

        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            var log = new SmsLog
            {
                NotificationId = request.NotificationId,
                Phone = request.Phone.Trim(),
                Body = request.Body,
                Status = SmsStatuses.Sent,
                RecipientId = request.RecipientId,
                RecipientName = request.RecipientName,
                EventCode = request.EventCode,
                SentAt = _clock.LocalNow,
            };

            context.SmsLogs.Add(log);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return Result<SmsSendResult>.Success(
                new SmsSendResult(log.Id, log.Phone, log.Body));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to record an SMS log row.");
            return Result<SmsSendResult>.Fail(
                Failure.Database(TranslationKeys.Errors.SaveError, exception.Message));
        }
    }
}
