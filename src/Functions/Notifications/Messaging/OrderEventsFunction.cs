using Azure.Messaging.ServiceBus;
using BuildingBlocks.Contracts;
using BuildingBlocks.Messaging.Diagnostics;
using Microsoft.Azure.Functions.Worker;

namespace Notifications.Messaging;

/// <summary>
/// The Service Bus trigger: the Functions host receives from the <c>notifications</c> subscription and calls this.
/// </summary>
/// <remarks>
/// The function is deliberately thin. It binds the raw <see cref="ServiceBusReceivedMessage"/> (the handler needs
/// the <c>MessageId</c> for the inbox and the <c>Subject</c> to know which event it is), re-attaches the
/// distributed trace that travelled in the message, and delegates. With <c>autoCompleteMessages</c> the host
/// completes the message when this returns and abandons it when it throws, so retries and dead-lettering stay
/// the subscription's job instead of being reimplemented here.
/// </remarks>
internal sealed class OrderEventsFunction(OrderEventHandler handler)
{
    [Function(nameof(OrderEvents))]
    public async Task OrderEvents(
        [ServiceBusTrigger(
            Topology.Topics.OrderEvents,
            Topology.Subscriptions.Notifications,
            Connection = Topology.ServiceBusConnectionName)]
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        message.ApplicationProperties.TryGetValue(MessagingDiagnostics.TraceParentProperty, out var traceParent);
        using var activity = MessagingDiagnostics.StartProcess(
            Topology.Subscriptions.Notifications,
            message.Subject,
            traceParent as string);

        await handler.HandleAsync(message, cancellationToken);
    }
}
