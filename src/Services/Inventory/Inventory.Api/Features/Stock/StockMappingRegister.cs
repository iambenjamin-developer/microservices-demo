using Inventory.Api.Domain;
using Mapster;

namespace Inventory.Api.Features.Stock;

/// <summary>
/// Mapster configuration for the stock area. Mappings only go entity → DTO: stock rows are created and changed
/// through <see cref="StockItem"/>'s own methods, never by mapping a request onto them. Written as plain
/// expressions so the same configuration serves in-memory <c>Map</c> and SQL-translated <c>ProjectToType</c>.
/// </summary>
public sealed class StockMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config) =>
        config.NewConfig<StockItem, StockResponse>()
            .Map(dest => dest.Sku, src => src.Sku)
            .Map(dest => dest.QuantityAvailable, src => src.QuantityAvailable)
            .Map(dest => dest.QuantityReserved, src => src.QuantityReserved)
            // Derived, not stored: computed by PostgreSQL when the query is a projection.
            .Map(dest => dest.QuantityOnHand, src => src.QuantityAvailable + src.QuantityReserved)
            .Map(dest => dest.UpdatedOnUtc, src => src.UpdatedOnUtc);
}
