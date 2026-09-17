namespace Notifications.Domain;

/// <summary>
/// One message for a point of sale: the outcome of an order, ready to be shown in the panel and to be e-mailed.
/// It is a flat read model on purpose — Notifications has no business rules, it records what other services decided.
/// </summary>
/// <remarks>
/// The title and the body are rendered once, when the event arrives, and then stored. Re-rendering them on
/// read would mean the panel could silently change what a customer was already told.
/// </remarks>
public sealed class Notification
{
    public const int CustomerIdMaxLength = 100;
    public const int EmailMaxLength = 320;
    public const int TitleMaxLength = 200;
    public const int BodyMaxLength = 2000;

    private Notification()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>The order this notification is about; also the correlation id of the whole saga.</summary>
    public Guid OrderId { get; private set; }

    public string CustomerId { get; private set; } = string.Empty;

    public string CustomerEmail { get; private set; } = string.Empty;

    public NotificationType Type { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    /// <summary>When the order outcome happened, as stamped by Ordering.</summary>
    public DateTimeOffset OccurredOnUtc { get; private set; }

    /// <summary>When this service stored the notification.</summary>
    public DateTimeOffset CreatedOnUtc { get; private set; }

    public EmailStatus EmailStatus { get; private set; }

    public DateTimeOffset? EmailSentOnUtc { get; private set; }

    /// <summary>Why the last delivery attempt failed, kept short for the panel and the logs.</summary>
    public string? EmailError { get; private set; }

    public static Notification Create(
        Guid orderId,
        string customerId,
        string customerEmail,
        NotificationType type,
        string title,
        string body,
        DateTimeOffset occurredOnUtc,
        DateTimeOffset createdOnUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(customerEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        return new Notification
        {
            // Version 7: time-ordered, so the panel's "newest first" is an index scan and not a sort.
            Id = Guid.CreateVersion7(),
            OrderId = orderId,
            CustomerId = customerId,
            CustomerEmail = customerEmail,
            Type = type,
            Title = Truncate(title, TitleMaxLength),
            Body = Truncate(body, BodyMaxLength),
            OccurredOnUtc = occurredOnUtc,
            CreatedOnUtc = createdOnUtc,
            EmailStatus = EmailStatus.Pending,
        };
    }

    public void MarkEmailSent(DateTimeOffset utcNow)
    {
        EmailStatus = EmailStatus.Sent;
        EmailSentOnUtc = utcNow;
        EmailError = null;
    }

    /// <summary>Delivery is turned off, which is a configured choice and not a failure.</summary>
    public void MarkEmailSkipped()
    {
        EmailStatus = EmailStatus.Skipped;
        EmailSentOnUtc = null;
        EmailError = null;
    }

    public void MarkEmailFailed(string error)
    {
        EmailStatus = EmailStatus.Failed;
        EmailSentOnUtc = null;
        EmailError = Truncate(error, BodyMaxLength);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
