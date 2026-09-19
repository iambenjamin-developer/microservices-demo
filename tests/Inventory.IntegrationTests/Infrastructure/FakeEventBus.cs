using System.Collections.Concurrent;
using BuildingBlocks.Messaging;

namespace Inventory.IntegrationTests.Infrastructure;

/// <summary>Records what the outbox processor publishes instead of sending it to Service Bus.</summary>
public sealed class FakeEventBus : IEventBus
{
    private readonly ConcurrentQueue<OutgoingMessage> _published = new();

    public IReadOnlyCollection<OutgoingMessage> Published => _published;

    public Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken)
    {
        _published.Enqueue(message);
        return Task.CompletedTask;
    }
}
