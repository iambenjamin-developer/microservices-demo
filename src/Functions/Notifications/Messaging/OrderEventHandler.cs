using System.Data.Common;
using Azure.Messaging.ServiceBus;
using BuildingBlocks.Contracts;
using BuildingBlocks.Contracts.Ordering;
using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Notifications.Domain;
using Notifications.Email;
using Notifications.Persistence;

namespace Notifications.Messaging;

/// <summary>
/// The notifications half of the choreography: an order was confirmed or rejected, so the point of sale is told.
/// </summary>
/// <remarks>
/// <para>
/// The Functions host owns the receive loop, so the idempotency the other services get from the shared consumer
/// pipeline is written out here: the notification and an <see cref="InboxMessage"/> keyed by the broker's
/// <c>MessageId</c> are saved in the same transaction, and a redelivery finds the inbox row and stops.
/// </para>
/// <para>
/// The e-mail is sent <i>after</i> that commit, on purpose. An SMTP send cannot be rolled back, so keeping it
/// inside the transaction would either lose notifications (rollback after a successful send) or send twice on
/// every retry. Storing first makes the panel the source of truth and the e-mail a best-effort second channel
/// whose outcome is recorded on the row.
/// </para>
/// </remarks>
internal sealed partial class OrderEventHandler(
    NotificationsDbContext dbContext,
    IEmailSender emailSender,
    TimeProvider timeProvider,
    ILogger<OrderEventHandler> logger)
{
    /// <summary>Inbox rows are keyed by message <i>and</i> consumer, so this name must stay stable.</summary>
    public const string Consumer = Topology.Subscriptions.Notifications;

    private const string PostgresUniqueViolation = "23505";

    public async Task HandleAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        // Defensive: the subscription rules only pass the two order outcomes, but an unknown Subject must
        // never block the subscription. Returning completes the message instead of retrying it forever.
        if (message.Subject is not (nameof(OrderConfirmed) or nameof(OrderRejected)))
        {
            LogUnknownSubject(logger, message.Subject);
            return;
        }

        // Anything this service cannot even identify is a poison message: throwing abandons it, and after the
        // subscription's max delivery count the broker moves it to the dead-letter queue for inspection.
        if (!Guid.TryParse(message.MessageId, out var messageId))
        {
            throw new InvalidOperationException($"Message id '{message.MessageId}' is not a GUID.");
        }

        if (await WasProcessedAsync(messageId, cancellationToken))
        {
            LogDuplicate(logger, message.Subject, messageId);
            return;
        }

        var notification = CreateNotification(message, timeProvider.GetUtcNow());

        dbContext.Notifications.Add(notification);
        dbContext.Set<InboxMessage>().Add(new InboxMessage
        {
            MessageId = messageId,
            Consumer = Consumer,
            ProcessedOnUtc = timeProvider.GetUtcNow(),
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is DbException { SqlState: PostgresUniqueViolation })
        {
            // A concurrent delivery of the same message may have won the race and committed first.
            // Any other unique violation is a real error: rethrow so the message is retried.
            if (!await WasProcessedAsync(messageId, cancellationToken))
            {
                throw;
            }

            LogDuplicate(logger, message.Subject, messageId);
            return;
        }

        LogStored(logger, message.Subject, notification.OrderId, messageId);

        await DeliverEmailAsync(notification, cancellationToken);
    }

    private static Notification CreateNotification(ServiceBusReceivedMessage message, DateTimeOffset utcNow) =>
        message.Subject switch
        {
            nameof(OrderConfirmed) => NotificationFactory.FromOrderConfirmed(Deserialize<OrderConfirmed>(message), utcNow),
            nameof(OrderRejected) => NotificationFactory.FromOrderRejected(Deserialize<OrderRejected>(message), utcNow),
            _ => throw new InvalidOperationException($"Subject '{message.Subject}' has no notification."),
        };

    private static TEvent Deserialize<TEvent>(ServiceBusReceivedMessage message)
        where TEvent : IntegrationEvent =>
        IntegrationEventSerializer.Deserialize<TEvent>(message.Body)
            ?? throw new InvalidOperationException($"The payload of message '{message.MessageId}' could not be read.");

    private Task<bool> WasProcessedAsync(Guid messageId, CancellationToken cancellationToken) =>
        dbContext.Set<InboxMessage>()
            .AnyAsync(m => m.MessageId == messageId && m.Consumer == Consumer, cancellationToken);

    /// <summary>
    /// Best effort by design: a failure is written on the notification and logged, never rethrown. Retrying the
    /// message would find the inbox row and skip it, so throwing here would only dead-letter an order outcome
    /// that was already recorded correctly.
    /// </summary>
    private async Task DeliverEmailAsync(Notification notification, CancellationToken cancellationToken)
    {
        try
        {
            var delivery = await emailSender.SendAsync(
                new EmailMessage(notification.CustomerEmail, notification.Title, notification.Body),
                cancellationToken);

            if (delivery is EmailDelivery.Sent)
            {
                notification.MarkEmailSent(timeProvider.GetUtcNow());
            }
            else
            {
                notification.MarkEmailSkipped();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            notification.MarkEmailFailed(ex.Message);
            LogEmailFailed(logger, ex, notification.CustomerEmail, notification.OrderId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Stored {Subject} notification for order {OrderId} (message {MessageId})")]
    private static partial void LogStored(ILogger logger, string subject, Guid orderId, Guid messageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Skipped duplicate {Subject} {MessageId}")]
    private static partial void LogDuplicate(ILogger logger, string subject, Guid messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No notification is defined for subject {Subject}; message completed")]
    private static partial void LogUnknownSubject(ILogger logger, string subject);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not e-mail {Recipient} about order {OrderId}; the notification is stored anyway")]
    private static partial void LogEmailFailed(ILogger logger, Exception exception, string recipient, Guid orderId);
}
