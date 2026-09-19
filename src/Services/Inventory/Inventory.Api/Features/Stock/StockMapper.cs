using Inventory.Api.Domain;
using Riok.Mapperly.Abstractions;

namespace Inventory.Api.Features.Stock;

/// <summary>
/// Mapperly mappings for the stock area, generated at compile time. Mappings only go entity → DTO: stock rows are
/// created and changed through <see cref="StockItem"/>'s own methods, never by mapping a request onto them.
/// </summary>
/// <remarks>
/// Strict on purpose: <see cref="RequiredMappingStrategy.Target"/> makes a destination member without a source a
/// warning, and warnings are errors. The projection reuses the element mapping, so in-memory maps and SQL share one
/// definition.
/// </remarks>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
internal static partial class StockMapper
{
    [MapPropertyFromSource(nameof(StockResponse.QuantityOnHand), Use = nameof(QuantityOnHand))]
    public static partial StockResponse ToResponse(this StockItem stockItem);

    /// <summary>Read side: translated by EF Core into the SELECT list, no entity is materialized.</summary>
    public static partial IQueryable<StockResponse> ProjectToResponse(this IQueryable<StockItem> query);

    // Derived, not stored: inlined into the projection, so PostgreSQL computes it.
    private static int QuantityOnHand(StockItem stockItem) => stockItem.QuantityAvailable + stockItem.QuantityReserved;
}
