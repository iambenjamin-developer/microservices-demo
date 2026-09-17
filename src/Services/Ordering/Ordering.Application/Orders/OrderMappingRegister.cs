using Mapster;
using Ordering.Domain.Orders;

namespace Ordering.Application.Orders;

/// <summary>
/// Mapster configuration for the order area. Mappings only go aggregate → DTO: orders are always created
/// through <see cref="Order.Place"/>, never mapped from a request. Every member is written as a plain
/// expression, so the same configuration serves in-memory <c>Map</c> and SQL-translated <c>ProjectToType</c>.
/// </summary>
public sealed class OrderMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<OrderItem, OrderItemResponse>()
            .Map(dest => dest.Sku, src => src.Sku.Value)
            .Map(dest => dest.ProductName, src => src.ProductName)
            .Map(dest => dest.UnitPrice, src => src.UnitPrice.Amount)
            .Map(dest => dest.Quantity, src => src.Quantity.Value)
            // LineTotal is computed in the domain and not stored; recomputed here so it also works in SQL.
            // Rounded so PostgreSQL returns cents (numeric multiplication would widen the scale).
            .Map(dest => dest.LineTotal, src => Math.Round(src.UnitPrice.Amount * src.Quantity.Value, 2));

        config.NewConfig<Order, OrderResponse>()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.CustomerId, src => src.CustomerId)
            .Map(dest => dest.Status, src => src.Status)
            // Items have no position column; sorting by SKU makes the response deterministic in SQL and in memory.
            .Map(dest => dest.Items, src => src.Items.OrderBy(item => item.Sku.Value))
            .Map(dest => dest.Subtotal, src => src.Subtotal.Amount)
            .Map(dest => dest.Discount, src => src.Discount.Amount)
            .Map(dest => dest.Total, src => src.Total.Amount)
            .Map(dest => dest.Currency, src => src.Total.Currency)
            .Map(dest => dest.RejectionReason, src => src.RejectionReason)
            .Map(dest => dest.PlacedOnUtc, src => src.PlacedOnUtc)
            .Map(dest => dest.CompletedOnUtc, src => src.CompletedOnUtc);

        config.NewConfig<Order, OrderSummaryResponse>()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.Status, src => src.Status)
            .Map(dest => dest.ItemCount, src => src.Items.Count)
            .Map(dest => dest.Total, src => src.Total.Amount)
            .Map(dest => dest.Currency, src => src.Total.Currency)
            .Map(dest => dest.PlacedOnUtc, src => src.PlacedOnUtc)
            .Map(dest => dest.CompletedOnUtc, src => src.CompletedOnUtc);
    }
}
