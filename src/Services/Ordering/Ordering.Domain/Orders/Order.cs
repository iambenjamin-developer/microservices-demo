using BuildingBlocks.Common.Results;
using Ordering.Domain.Abstractions;
using Ordering.Domain.Discounts;
using Ordering.Domain.Orders.Events;
using Ordering.Domain.ValueObjects;

namespace Ordering.Domain.Orders;

/// <summary>
/// Aggregate root of the ordering context. It owns its <see cref="OrderItem"/>s, fixes prices at
/// placement time and moves through a guarded state machine: <c>Pending → Confirmed | Rejected</c>.
/// </summary>
/// <remarks>
/// Guard clauses (exceptions) protect against programming errors, such as a missing customer id taken
/// from the token. Business rule violations a caller can trigger are returned as <see cref="Result"/> errors.
/// </remarks>
public sealed class Order : AggregateRoot<Guid>
{
    public const int MaxItems = 50;

    private static readonly IReadOnlyDictionary<OrderStatus, OrderStatus[]> _allowedTransitions = new Dictionary<OrderStatus, OrderStatus[]>
    {
        [OrderStatus.Pending] = [OrderStatus.Confirmed, OrderStatus.Rejected],
        [OrderStatus.Confirmed] = [],
        [OrderStatus.Rejected] = [],
    };

    private readonly List<OrderItem> _items = [];

    private Order(Guid id, string customerId, string customerEmail, Money subtotal, Money discount, DateTimeOffset placedOnUtc)
        : base(id)
    {
        CustomerId = customerId;
        CustomerEmail = customerEmail;
        Status = OrderStatus.Pending;
        Subtotal = subtotal;
        Discount = discount;
        Total = subtotal.Subtract(discount);
        PlacedOnUtc = placedOnUtc;
    }

    // Required by EF Core materialization.
    private Order()
    {
        CustomerId = null!;
        CustomerEmail = null!;
        Subtotal = null!;
        Discount = null!;
        Total = null!;
    }

    public string CustomerId { get; private set; }

    public string CustomerEmail { get; private set; }

    public OrderStatus Status { get; private set; }

    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();

    public Money Subtotal { get; private set; }

    public Money Discount { get; private set; }

    public Money Total { get; private set; }

    public string? RejectionReason { get; private set; }

    public DateTimeOffset PlacedOnUtc { get; private set; }

    /// <summary>When the order reached a terminal state (confirmed or rejected).</summary>
    public DateTimeOffset? CompletedOnUtc { get; private set; }

    /// <summary>
    /// Factory method: the only way to create an order, so an <see cref="Order"/> instance is always valid.
    /// </summary>
    public static Result<Order> Place(
        string customerId,
        string customerEmail,
        IReadOnlyCollection<OrderLine> lines,
        IDiscountPolicy discountPolicy,
        DateTimeOffset placedOnUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(customerEmail);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(discountPolicy);
        if (lines.Any(line => line is null))
        {
            throw new ArgumentException("Order lines cannot contain null entries.", nameof(lines));
        }

        var validation = ValidateLines(lines);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        var items = lines.Select(OrderItem.From).ToList();
        var currency = items[0].UnitPrice.Currency;

        var subtotal = items.Aggregate(Money.Zero(currency), (sum, item) => sum.Add(item.LineTotal));
        var totalQuantity = items.Sum(item => item.Quantity.Value);
        var discount = discountPolicy.CalculateDiscount(subtotal, totalQuantity);
        EnsureValidDiscount(discount, subtotal);

        var order = new Order(Guid.CreateVersion7(), customerId.Trim(), customerEmail.Trim(), subtotal, discount, placedOnUtc);
        order._items.AddRange(items);

        order.Raise(new OrderPlacedDomainEvent(
            order.Id,
            order.CustomerId,
            [.. items.Select(item => new OrderPlacedDomainEventItem(item.Sku.Value, item.Quantity.Value))],
            placedOnUtc));

        return order;
    }

    /// <summary>Inventory reserved the stock.</summary>
    public Result Confirm(DateTimeOffset confirmedOnUtc)
    {
        var transition = TransitionTo(OrderStatus.Confirmed, confirmedOnUtc);
        if (transition.IsFailure)
        {
            return transition;
        }

        Raise(new OrderConfirmedDomainEvent(Id, CustomerId, CustomerEmail, Total, confirmedOnUtc));
        return Result.Success();
    }

    /// <summary>Inventory could not reserve the stock (compensation path of the saga).</summary>
    public Result Reject(string reason, DateTimeOffset rejectedOnUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var transition = TransitionTo(OrderStatus.Rejected, rejectedOnUtc);
        if (transition.IsFailure)
        {
            return transition;
        }

        RejectionReason = reason.Trim();
        Raise(new OrderRejectedDomainEvent(Id, CustomerId, CustomerEmail, RejectionReason, rejectedOnUtc));
        return Result.Success();
    }

    private static Result ValidateLines(IReadOnlyCollection<OrderLine> lines)
    {
        if (lines.Count == 0)
        {
            return OrderErrors.NoItems;
        }

        if (lines.Count > MaxItems)
        {
            return OrderErrors.TooManyItems;
        }

        var duplicate = lines.GroupBy(line => line.Sku).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            return OrderErrors.DuplicateSku(duplicate.Key.Value);
        }

        if (lines.Select(line => line.UnitPrice.Currency).Distinct().Count() > 1)
        {
            return OrderErrors.MixedCurrencies;
        }

        return Result.Success();
    }

    private static void EnsureValidDiscount(Money discount, Money subtotal)
    {
        if (discount is null || discount.Currency != subtotal.Currency || discount.Amount > subtotal.Amount)
        {
            throw new InvalidOperationException(
                $"The discount policy returned '{discount}' for a subtotal of '{subtotal}'; a discount must use the same currency and cannot exceed the subtotal.");
        }
    }

    private Result TransitionTo(OrderStatus next, DateTimeOffset occurredOnUtc)
    {
        if (!_allowedTransitions[Status].Contains(next))
        {
            return OrderErrors.InvalidStatusTransition(Id, Status, next);
        }

        Status = next;
        CompletedOnUtc = occurredOnUtc;
        return Result.Success();
    }
}
