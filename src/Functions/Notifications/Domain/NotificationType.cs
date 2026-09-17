namespace Notifications.Domain;

/// <summary>What happened to the order. It mirrors the integration event that produced the notification.</summary>
public enum NotificationType
{
    OrderConfirmed,
    OrderRejected,
}
