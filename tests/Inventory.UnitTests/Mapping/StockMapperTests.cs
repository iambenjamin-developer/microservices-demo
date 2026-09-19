using Inventory.Api.Domain;
using Inventory.Api.Features.Stock;

namespace Inventory.UnitTests.Mapping;

// Unmapped members are compile errors (Mapperly + warnings as errors); these tests check the values.
public sealed class StockMapperTests
{
    private static readonly DateTimeOffset _updatedOn = new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ToResponse_ReservedStockItem_ComputesQuantityOnHand()
    {
        var stockItem = ReservedStockItem();

        var response = stockItem.ToResponse();

        response.ShouldBe(new StockResponse("GOLDEN-LAGER-350", 70, 30, 100, _updatedOn));
    }

    [Fact]
    public void ProjectToResponse_ReservedStockItem_MatchesInMemoryMapping()
    {
        var stockItem = ReservedStockItem();

        var projected = new[] { stockItem }.AsQueryable().ProjectToResponse().Single();

        projected.ShouldBe(stockItem.ToResponse());
    }

    private static StockItem ReservedStockItem()
    {
        var stockItem = StockItem.Create("GOLDEN-LAGER-350", 100, _updatedOn);
        stockItem.Reserve(30, _updatedOn);
        return stockItem;
    }
}
