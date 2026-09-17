using BuildingBlocks.Common.Results;
using MapsterMapper;
using Microsoft.Extensions.Options;
using Ordering.Application.Abstractions.Catalog;
using Ordering.Application.Abstractions.Messaging;
using Ordering.Application.Abstractions.Persistence;
using Ordering.Application.Pricing;
using Ordering.Domain.Discounts;
using Ordering.Domain.Orders;
using Ordering.Domain.ValueObjects;

namespace Ordering.Application.Orders.PlaceOrder;

/// <summary>
/// Snapshots prices from Catalog, lets the <see cref="Order"/> aggregate enforce its rules and saves it.
/// The <c>OrderPlaced</c> integration event is stored in the outbox by the same commit, so the order is
/// never saved without its event (or the other way round).
/// </summary>
internal sealed class PlaceOrderCommandHandler(
    ICatalogClient catalogClient,
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork,
    IDiscountPolicy discountPolicy,
    IOptions<PricingOptions> pricingOptions,
    TimeProvider timeProvider,
    IMapper mapper) : ICommandHandler<PlaceOrderCommand, OrderResponse>
{
    public async Task<Result<OrderResponse>> HandleAsync(PlaceOrderCommand command, CancellationToken cancellationToken)
    {
        var skus = command.Items.Select(item => Normalize(item.Sku)).ToArray();

        var catalogResult = await catalogClient.GetProductsAsync(skus, cancellationToken);
        if (catalogResult.IsFailure)
        {
            return catalogResult.Error;
        }

        var products = catalogResult.Value.ToDictionary(product => Normalize(product.Sku), StringComparer.Ordinal);

        var unknownSkus = skus.Where(sku => !products.ContainsKey(sku)).ToArray();
        if (unknownSkus.Length > 0)
        {
            return PlaceOrderErrors.UnknownProducts(unknownSkus);
        }

        var lines = new List<OrderLine>(command.Items.Count);
        foreach (var item in command.Items)
        {
            var line = CreateLine(item, products[Normalize(item.Sku)], pricingOptions.Value.Currency);
            if (line.IsFailure)
            {
                return line.Error;
            }

            lines.Add(line.Value);
        }

        var orderResult = Order.Place(
            command.CustomerId,
            command.CustomerEmail,
            lines,
            discountPolicy,
            timeProvider.GetUtcNow());

        if (orderResult.IsFailure)
        {
            return orderResult.Error;
        }

        orderRepository.Add(orderResult.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<OrderResponse>(orderResult.Value);
    }

    private static Result<OrderLine> CreateLine(PlaceOrderItem item, CatalogProduct product, string currency)
    {
        var sku = Sku.Create(item.Sku);
        if (sku.IsFailure)
        {
            return sku.Error;
        }

        var quantity = Quantity.Create(item.Quantity);
        if (quantity.IsFailure)
        {
            return quantity.Error;
        }

        var unitPrice = Money.Create(product.Price, currency);
        if (unitPrice.IsFailure)
        {
            return unitPrice.Error;
        }

        return new OrderLine(sku.Value, product.Name, unitPrice.Value, quantity.Value);
    }

    private static string Normalize(string sku) => sku.Trim().ToUpperInvariant();
}
