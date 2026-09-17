using Ordering.Domain.Abstractions;
using Ordering.Domain.ValueObjects;

namespace Ordering.Domain.Orders.Events;

public sealed record OrderConfirmedDomainEvent(
    Guid OrderId,
    string CustomerId,
    string CustomerEmail,
    Money Total,
    DateTimeOffset OccurredOnUtc) : DomainEvent(OccurredOnUtc);
