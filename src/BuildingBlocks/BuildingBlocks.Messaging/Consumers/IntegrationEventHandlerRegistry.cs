using BuildingBlocks.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Messaging.Consumers;

/// <summary>
/// Maps a message Subject (the event name) to its CLR type and to a strongly typed handler invocation,
/// so the processor can dispatch without reflection at runtime.
/// </summary>
internal sealed class IntegrationEventHandlerRegistry
{
    private readonly Dictionary<string, Registration> _registrations = new(StringComparer.Ordinal);

    public void Register<TEvent>()
        where TEvent : IntegrationEvent
    {
        _registrations[Topology.SubjectFor(typeof(TEvent))] = new Registration(
            typeof(TEvent),
            (services, integrationEvent, context, cancellationToken) => services
                .GetRequiredService<IIntegrationEventHandler<TEvent>>()
                .HandleAsync((TEvent)integrationEvent, context, cancellationToken));
    }

    public bool TryGet(string subject, out Registration registration) =>
        _registrations.TryGetValue(subject, out registration!);

    internal sealed record Registration(
        Type EventType,
        Func<IServiceProvider, IntegrationEvent, IntegrationEventContext, CancellationToken, Task> InvokeAsync);
}
