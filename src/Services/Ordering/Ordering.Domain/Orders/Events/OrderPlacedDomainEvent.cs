using Ordering.Domain.Abstractions;

namespace Ordering.Domain.Orders.Events;

public sealed record OrderPlacedDomainEvent(
    Guid OrderId,
    string CustomerId,
    IReadOnlyList<OrderPlacedDomainEventItem> Items,
    DateTimeOffset OccurredOnUtc) : DomainEvent(OccurredOnUtc);

public sealed record OrderPlacedDomainEventItem(string Sku, int Quantity);
