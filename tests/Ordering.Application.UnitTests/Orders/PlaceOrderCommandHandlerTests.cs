using BuildingBlocks.Common.Results;
using MapsterMapper;
using Microsoft.Extensions.Options;
using NSubstitute;
using Ordering.Application.Abstractions.Catalog;
using Ordering.Application.Abstractions.Persistence;
using Ordering.Application.Orders.PlaceOrder;
using Ordering.Application.Pricing;
using Ordering.Domain.Discounts;
using Ordering.Domain.Orders;
using Ordering.Domain.ValueObjects;

namespace Ordering.Application.UnitTests.Orders;

public sealed class PlaceOrderCommandHandlerTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 17, 15, 30, 0, TimeSpan.Zero);

    private readonly ICatalogClient _catalogClient = Substitute.For<ICatalogClient>();
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();

    public PlaceOrderCommandHandlerTests() => _timeProvider.GetUtcNow().Returns(_now);

    [Fact]
    public async Task HandleAsync_KnownProducts_SavesPendingOrderWithCatalogPriceSnapshot()
    {
        GivenCatalog(new CatalogProduct("GOLDEN-LAGER-350", "Golden Lager 350ml", 12.00m));
        var handler = CreateHandler(currency: "EUR");

        var result = await handler.HandleAsync(Command(("golden-lager-350", 4)), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(OrderStatus.Pending);
        result.Value.Total.ShouldBe(48.00m);
        result.Value.Currency.ShouldBe("EUR");
        result.Value.PlacedOnUtc.ShouldBe(_now);
        result.Value.Items.ShouldHaveSingleItem().ProductName.ShouldBe("Golden Lager 350ml");

        _orderRepository.Received(1).Add(Arg.Is<Order>(order => order.Id == result.Value.Id && order.CustomerId == "pos-001"));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_CatalogCalledOnceWithNormalizedSkus()
    {
        GivenCatalog(
            new CatalogProduct("GOLDEN-LAGER-350", "Golden Lager 350ml", 12.00m),
            new CatalogProduct("AMBER-ALE-600", "Amber Ale 600ml", 27.60m));
        var handler = CreateHandler();

        await handler.HandleAsync(Command((" golden-lager-350 ", 1), ("Amber-Ale-600", 1)), TestContext.Current.CancellationToken);

        await _catalogClient.Received(1).GetProductsAsync(
            Arg.Is<IReadOnlyCollection<string>>(skus => skus.SequenceEqual(new[] { "GOLDEN-LAGER-350", "AMBER-ALE-600" })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ReachingVolumeTier_AppliesConfiguredDiscount()
    {
        GivenCatalog(new CatalogProduct("SUNNY-WHEAT-330", "Sunny Wheat 330ml", 31.20m));
        var handler = CreateHandler(discountPolicy: new VolumeDiscountPolicy([new VolumeDiscountTier(20, 0.05m)]));

        var result = await handler.HandleAsync(Command(("SUNNY-WHEAT-330", 20)), TestContext.Current.CancellationToken);

        result.Value.Subtotal.ShouldBe(624.00m);
        result.Value.Discount.ShouldBe(31.20m);
        result.Value.Total.ShouldBe(592.80m);
    }

    [Fact]
    public async Task HandleAsync_UnknownSku_ReturnsUnknownProductsErrorAndSavesNothing()
    {
        GivenCatalog(new CatalogProduct("GOLDEN-LAGER-350", "Golden Lager 350ml", 12.00m));
        var handler = CreateHandler();

        var result = await handler.HandleAsync(
            Command(("GOLDEN-LAGER-350", 1), ("GHOST-BEER-1", 1)),
            TestContext.Current.CancellationToken);

        result.Error.Code.ShouldBe("Orders.UnknownProducts");
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Description.ShouldContain("GHOST-BEER-1");
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task HandleAsync_CatalogUnavailable_ReturnsUnavailableErrorAndSavesNothing()
    {
        _catalogClient.GetProductsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns(CatalogErrors.Unavailable);
        var handler = CreateHandler();

        var result = await handler.HandleAsync(Command(("GOLDEN-LAGER-350", 1)), TestContext.Current.CancellationToken);

        result.Error.ShouldBe(CatalogErrors.Unavailable);
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task HandleAsync_CatalogPriceWithMoreThanTwoDecimals_ReturnsMoneyErrorAndSavesNothing()
    {
        GivenCatalog(new CatalogProduct("GOLDEN-LAGER-350", "Golden Lager 350ml", 12.001m));
        var handler = CreateHandler();

        var result = await handler.HandleAsync(Command(("GOLDEN-LAGER-350", 1)), TestContext.Current.CancellationToken);

        result.Error.ShouldBe(MoneyErrors.InvalidScale);
        await AssertNothingSavedAsync();
    }

    private PlaceOrderCommandHandler CreateHandler(string currency = "USD", IDiscountPolicy? discountPolicy = null) =>
        new(
            _catalogClient,
            _orderRepository,
            _unitOfWork,
            discountPolicy ?? NoDiscountPolicy.Instance,
            Options.Create(new PricingOptions { Currency = currency }),
            _timeProvider,
            new Mapper(DependencyInjection.CreateMappingConfig()));

    private void GivenCatalog(params CatalogProduct[] products) =>
        _catalogClient.GetProductsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<CatalogProduct>>(products));

    private async Task AssertNothingSavedAsync()
    {
        _orderRepository.DidNotReceiveWithAnyArgs().Add(default!);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static PlaceOrderCommand Command(params (string Sku, int Quantity)[] items) =>
        new("pos-001", "bar@example.com", [.. items.Select(item => new PlaceOrderItem(item.Sku, item.Quantity))]);
}
