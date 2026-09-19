using Ordering.Domain.Orders;
using Riok.Mapperly.Abstractions;

namespace Ordering.Application.Orders;

/// <summary>
/// Mapperly mappings for the order area, generated at compile time. Mappings only go aggregate → DTO: orders are
/// always created through <see cref="Order.Place"/>, never mapped from a request.
/// </summary>
/// <remarks>
/// <para>
/// Strict on purpose: <see cref="RequiredMappingStrategy.Target"/> makes a destination member without a source a
/// warning, and warnings are errors, so a renamed property breaks the build instead of returning a default value.
/// </para>
/// <para>
/// The <c>Project*</c> methods reuse the element mappings, so in-memory maps and SQL projections share one
/// definition. Computed members are expression-bodied helpers that Mapperly inlines into the query expression.
/// </para>
/// </remarks>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class OrderMapper
{
    [MapProperty("Subtotal.Amount", nameof(OrderResponse.Subtotal))]
    [MapProperty("Discount.Amount", nameof(OrderResponse.Discount))]
    [MapProperty("Total.Amount", nameof(OrderResponse.Total))]
    [MapProperty("Total.Currency", nameof(OrderResponse.Currency))]
    [MapProperty(nameof(Order.Items), nameof(OrderResponse.Items), Use = nameof(MapItemsBySku))]
    public static partial OrderResponse ToResponse(this Order order);

    [MapProperty("Total.Amount", nameof(OrderSummaryResponse.Total))]
    [MapProperty("Total.Currency", nameof(OrderSummaryResponse.Currency))]
    [MapPropertyFromSource(nameof(OrderSummaryResponse.ItemCount), Use = nameof(CountItems))]
    public static partial OrderSummaryResponse ToSummary(this Order order);

    /// <summary>Read side: translated by EF Core into the SELECT list, no aggregate is materialized.</summary>
    public static partial IQueryable<OrderResponse> ProjectToResponse(this IQueryable<Order> query);

    /// <summary>Row of the order list; the items are only counted, not loaded.</summary>
    public static partial IQueryable<OrderSummaryResponse> ProjectToSummary(this IQueryable<Order> query);

    [MapProperty("Sku.Value", nameof(OrderItemResponse.Sku))]
    [MapProperty("UnitPrice.Amount", nameof(OrderItemResponse.UnitPrice))]
    [MapProperty("Quantity.Value", nameof(OrderItemResponse.Quantity))]
    [MapPropertyFromSource(nameof(OrderItemResponse.LineTotal), Use = nameof(LineTotal))]
    private static partial OrderItemResponse MapItem(OrderItem item);

    // Items have no position column; sorting by SKU makes the response deterministic in SQL and in memory.
    private static IReadOnlyList<OrderItemResponse> MapItemsBySku(IReadOnlyList<OrderItem> items) =>
        items.OrderBy(item => item.Sku.Value).Select(item => MapItem(item)).ToList();

    // LineTotal is computed in the domain and not stored; recomputed here so it also works in SQL.
    // Rounded so PostgreSQL returns cents (numeric multiplication would widen the scale).
    private static decimal LineTotal(OrderItem item) => Math.Round(item.UnitPrice.Amount * item.Quantity.Value, 2);

    private static int CountItems(Order order) => order.Items.Count;
}
