using Microsoft.EntityFrameworkCore;
using Notifications.Persistence;

namespace Notifications.Api;

/// <summary>
/// The read side of the panel: the caller's notifications, newest first. Read-only and projected in SQL,
/// so the entity is never materialized just to be thrown away.
/// </summary>
internal sealed class GetNotificationsQuery(NotificationsDbContext dbContext)
{
    /// <summary>A panel is not a report: the page a point of sale actually looks at is small.</summary>
    public const int MaxResults = 100;

    public async Task<IReadOnlyList<NotificationResponse>> ExecuteAsync(string customerId, CancellationToken cancellationToken) =>
        await dbContext.Notifications
            .AsNoTracking()
            // Scoped to the caller in the query itself: a customer cannot read another one's notifications
            // even if it guesses an id, because there is no endpoint that takes one.
            .Where(notification => notification.CustomerId == customerId)
            .OrderByDescending(notification => notification.CreatedOnUtc)
            .Take(MaxResults)
            .Select(notification => new NotificationResponse(
                notification.Id,
                notification.OrderId,
                notification.Type,
                notification.Title,
                notification.Body,
                notification.OccurredOnUtc,
                notification.CreatedOnUtc,
                notification.EmailStatus,
                notification.EmailSentOnUtc))
            .ToListAsync(cancellationToken);
}
