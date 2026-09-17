using Ordering.Domain.Abstractions;

namespace Ordering.Domain.Orders.Events;

public sealed record OrderRejectedDomainEvent(
    Guid OrderId,
    string CustomerId,
    string CustomerEmail,
    string Reason,
    DateTimeOffset OccurredOnUtc) : DomainEvent(OccurredOnUtc);
