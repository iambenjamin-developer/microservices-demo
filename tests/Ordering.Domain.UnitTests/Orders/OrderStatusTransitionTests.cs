using Ordering.Domain.Orders;
using Ordering.Domain.Orders.Events;
using static Ordering.Domain.UnitTests.Builders.OrderBuilder;

namespace Ordering.Domain.UnitTests.Orders;

public sealed class OrderStatusTransitionTests
{
    private static readonly DateTimeOffset _now = DefaultPlacedOnUtc.AddSeconds(5);

    [Fact]
    public void Confirm_PendingOrder_ConfirmsAndRaisesOrderConfirmedDomainEvent()
    {
        var order = AnOrder().Build();

        var result = order.Confirm(_now);

        result.IsSuccess.ShouldBeTrue();
        order.Status.ShouldBe(OrderStatus.Confirmed);
        order.CompletedOnUtc.ShouldBe(_now);
        var domainEvent = order.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<OrderConfirmedDomainEvent>();
        domainEvent.OrderId.ShouldBe(order.Id);
        domainEvent.CustomerId.ShouldBe(order.CustomerId);
        domainEvent.CustomerEmail.ShouldBe(order.CustomerEmail);
        domainEvent.Total.ShouldBe(order.Total);
        domainEvent.OccurredOnUtc.ShouldBe(_now);
    }

    [Fact]
    public void Reject_PendingOrder_RejectsWithReasonAndRaisesOrderRejectedDomainEvent()
    {
        var order = AnOrder().Build();

        var result = order.Reject("  Insufficient stock for AMBER-ALE-600. ", _now);

        result.IsSuccess.ShouldBeTrue();
        order.Status.ShouldBe(OrderStatus.Rejected);
        order.RejectionReason.ShouldBe("Insufficient stock for AMBER-ALE-600.");
        order.CompletedOnUtc.ShouldBe(_now);
        var domainEvent = order.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<OrderRejectedDomainEvent>();
        domainEvent.OrderId.ShouldBe(order.Id);
        domainEvent.CustomerEmail.ShouldBe(order.CustomerEmail);
        domainEvent.Reason.ShouldBe("Insufficient stock for AMBER-ALE-600.");
        domainEvent.OccurredOnUtc.ShouldBe(_now);
    }

    [Fact]
    public void Confirm_AlreadyConfirmedOrder_ReturnsInvalidStatusTransitionAndRaisesNoEvent()
    {
        var order = AnOrder().Build();
        order.Confirm(_now);
        order.ClearDomainEvents();

        var result = order.Confirm(_now.AddMinutes(1));

        result.Error.ShouldBe(OrderErrors.InvalidStatusTransition(order.Id, OrderStatus.Confirmed, OrderStatus.Confirmed));
        order.CompletedOnUtc.ShouldBe(_now);
        order.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Confirm_RejectedOrder_ReturnsInvalidStatusTransition()
    {
        var order = AnOrder().Build();
        order.Reject("Out of stock.", _now);

        var result = order.Confirm(_now);

        result.Error.ShouldBe(OrderErrors.InvalidStatusTransition(order.Id, OrderStatus.Rejected, OrderStatus.Confirmed));
        order.Status.ShouldBe(OrderStatus.Rejected);
    }

    [Fact]
    public void Reject_ConfirmedOrder_ReturnsInvalidStatusTransitionAndKeepsNoReason()
    {
        var order = AnOrder().Build();
        order.Confirm(_now);

        var result = order.Reject("Out of stock.", _now);

        result.Error.ShouldBe(OrderErrors.InvalidStatusTransition(order.Id, OrderStatus.Confirmed, OrderStatus.Rejected));
        order.Status.ShouldBe(OrderStatus.Confirmed);
        order.RejectionReason.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Reject_BlankReason_ThrowsArgumentException(string reason)
    {
        var order = AnOrder().Build();

        Should.Throw<ArgumentException>(() => order.Reject(reason, _now));
        order.Status.ShouldBe(OrderStatus.Pending);
    }

    [Fact]
    public void ClearDomainEvents_AfterPlacement_RemovesAllEvents()
    {
        var order = AnOrder().Place().Value;
        order.DomainEvents.ShouldNotBeEmpty();

        order.ClearDomainEvents();

        order.DomainEvents.ShouldBeEmpty();
    }
}
