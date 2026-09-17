using BuildingBlocks.Common.Results;
using FluentValidation;
using NSubstitute;
using Ordering.Application.Abstractions.Messaging;
using Ordering.Application.Decorators;
using Ordering.Application.Orders;
using Ordering.Application.Orders.PlaceOrder;

namespace Ordering.Application.UnitTests.Decorators;

public sealed class ValidationDecoratorTests
{
    private readonly ICommandHandler<PlaceOrderCommand, OrderResponse> _inner =
        Substitute.For<ICommandHandler<PlaceOrderCommand, OrderResponse>>();

    [Fact]
    public async Task HandleAsync_InvalidCommand_ReturnsValidationErrorPerPropertyWithoutCallingHandler()
    {
        var decorator = new ValidationDecorator<PlaceOrderCommand, OrderResponse>(_inner, [new PlaceOrderCommandValidator()]);
        var command = new PlaceOrderCommand(
            "pos-001",
            "not-an-email",
            [new PlaceOrderItem("GOLDEN LAGER", 0)]);

        var result = await decorator.HandleAsync(command, TestContext.Current.CancellationToken);

        var error = result.Error.ShouldBeOfType<ValidationError>();
        error.Type.ShouldBe(ErrorType.Validation);
        error.Errors.Keys.ShouldBe(["CustomerEmail", "Items[0].Sku", "Items[0].Quantity"], ignoreOrder: true);
        await _inner.DidNotReceiveWithAnyArgs().HandleAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_CallsHandler()
    {
        var decorator = new ValidationDecorator<PlaceOrderCommand, OrderResponse>(_inner, [new PlaceOrderCommandValidator()]);
        var command = new PlaceOrderCommand("pos-001", "bar@example.com", [new PlaceOrderItem("GOLDEN-LAGER-350", 2)]);

        await decorator.HandleAsync(command, TestContext.Current.CancellationToken);

        await _inner.Received(1).HandleAsync(command, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NoValidators_CallsHandler()
    {
        var decorator = new ValidationDecorator<PlaceOrderCommand, OrderResponse>(_inner, Array.Empty<IValidator<PlaceOrderCommand>>());
        var command = new PlaceOrderCommand(string.Empty, string.Empty, []);

        await decorator.HandleAsync(command, TestContext.Current.CancellationToken);

        await _inner.Received(1).HandleAsync(command, Arg.Any<CancellationToken>());
    }
}
