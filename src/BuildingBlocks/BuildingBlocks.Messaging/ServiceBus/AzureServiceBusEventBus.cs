using System.Collections.Concurrent;
using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using BuildingBlocks.Messaging.Diagnostics;

namespace BuildingBlocks.Messaging.ServiceBus;

internal sealed class AzureServiceBusEventBus(ServiceBusClient client) : IEventBus, IAsyncDisposable
{
    // Senders are thread-safe and expensive to create: one per topic for the lifetime of the app.
    private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new();

    public async Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken)
    {
        using var activity = MessagingDiagnostics.StartPublish(message.Topic, message.Subject, message.TraceParent);

        var serviceBusMessage = new ServiceBusMessage(BinaryData.FromString(message.Payload))
        {
            MessageId = message.MessageId.ToString(),
            Subject = message.Subject,
            CorrelationId = message.CorrelationId,
            ContentType = "application/json",
        };

        if ((Activity.Current?.Id ?? message.TraceParent) is { } traceParent)
        {
            serviceBusMessage.ApplicationProperties[MessagingDiagnostics.TraceParentProperty] = traceParent;
        }

        var sender = _senders.GetOrAdd(message.Topic, client.CreateSender);
        await sender.SendMessageAsync(serviceBusMessage, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var sender in _senders.Values)
        {
            await sender.DisposeAsync();
        }
    }
}
