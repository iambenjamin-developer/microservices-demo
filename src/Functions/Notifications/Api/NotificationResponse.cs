using Notifications.Domain;

namespace Notifications.Api;

/// <summary>
/// One row of the notifications panel. Mapped by hand: this service has a single read model, so a mapping
/// library would add a dependency without removing any decision (Ordering and Inventory show the Mapperly side).
/// </summary>
/// <remarks>
/// <c>EmailStatus</c> says whether the second channel reached the customer; the panel shows the notification
/// either way. Enums are serialized as strings, like everywhere else in this system.
/// </remarks>
public sealed record NotificationResponse(
    Guid Id,
    Guid OrderId,
    NotificationType Type,
    string Title,
    string Body,
    DateTimeOffset OccurredOnUtc,
    DateTimeOffset CreatedOnUtc,
    EmailStatus EmailStatus,
    DateTimeOffset? EmailSentOnUtc);
