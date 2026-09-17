using System.Collections.Concurrent;
using BuildingBlocks.Messaging;

namespace Ordering.IntegrationTests.Infrastructure;

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

    public async Task<OutgoingMessage> WaitForAsync(Func<OutgoingMessage, bool> predicate, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        while (true)
        {
            if (_published.FirstOrDefault(predicate) is { } message)
            {
                return message;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        }
    }
}
