using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Time;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

public sealed class NotificationRepository
    : GenericRepository<Notification>, INotificationRepository
{
    public NotificationRepository(
        IDbContextFactory<SchoolManagementDbContext> contextFactory,
        ITrustedClock clock,
        ILogger<NotificationRepository> logger)
        : base(contextFactory, clock, logger)
    {
    }

    public Task<Result<IReadOnlyList<NotificationListItem>>> GetRecentAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        var pageSize = Math.Clamp(take, 1, 200);

        return RunAsync<IReadOnlyList<NotificationListItem>>(
            "Notifications.GetRecent",
            async context => await context.Notifications
                .AsNoTracking()
                .OrderByDescending(notification => notification.CreatedAt)
                .ThenByDescending(notification => notification.Id)
                .Take(pageSize)
                .Select(notification => new NotificationListItem(
                    notification.Id,
                    notification.Title,
                    notification.Body,
                    notification.Type,
                    notification.RecipientType,
                    notification.RecipientId,
                    notification.Channel,
                    notification.IsRead,
                    notification.Metadata,
                    notification.CreatedAt))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);
    }

    public Task<Result<int>> CountUnreadAsync(CancellationToken cancellationToken = default)
        => RunAsync(
            "Notifications.CountUnread",
            async context => await context.Notifications
                .AsNoTracking()
                .CountAsync(notification => !notification.IsRead, cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    public Task<Result<bool>> MarkReadAsync(int id, CancellationToken cancellationToken = default)
        => RunResultAsync(
            "Notifications.MarkRead",
            async context =>
            {
                var notification = await context.Notifications
                    .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                    .ConfigureAwait(false);

                if (notification is null)
                {
                    return Result<bool>.Fail(Failure.NotFound(TranslationKeys.Errors.NotFound));
                }

                notification.IsRead = true;
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return Result<bool>.Success(true);
            },
            cancellationToken,
            TranslationKeys.Errors.SaveError);

    public Task<Result<bool>> HasUnreadAsync(
        string type,
        int recipientId,
        CancellationToken cancellationToken = default)
        => RunAsync(
            "Notifications.HasUnread",
            async context => await context.Notifications
                .AsNoTracking()
                .AnyAsync(
                    notification => !notification.IsRead
                        && notification.Type == type
                        && notification.RecipientId == recipientId,
                    cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);
}
