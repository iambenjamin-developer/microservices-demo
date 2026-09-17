using Inventory.Api.Domain;
using Inventory.Api.Features.Stock;
using Inventory.Api.Mapping;
using MapsterMapper;

namespace Inventory.UnitTests.Mapping;

public sealed class StockMappingConfigTests
{
    [Fact]
    public void CreateMappingConfig_AllRegisters_CompileWithoutUnmappedMembers()
    {
        var config = InventoryMapping.CreateMappingConfig();

        Should.NotThrow(() => config.Compile());
    }

    [Fact]
    public void CreateMappingConfig_AllRegisters_CompileAsQueryProjections()
    {
        var config = InventoryMapping.CreateMappingConfig();

        Should.NotThrow(() => config.CompileProjection());
    }

    [Fact]
    public void Map_ReservedStockItem_ComputesQuantityOnHand()
    {
        var updatedOn = new DateTimeOffset(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);
        var mapper = new Mapper(InventoryMapping.CreateMappingConfig());
        var stockItem = StockItem.Create("GOLDEN-LAGER-350", 100, updatedOn);
        stockItem.Reserve(30, updatedOn);

        var response = mapper.Map<StockResponse>(stockItem);

        response.ShouldBe(new StockResponse("GOLDEN-LAGER-350", 70, 30, 100, updatedOn));
    }
}
