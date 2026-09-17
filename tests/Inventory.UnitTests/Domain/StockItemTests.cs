using Inventory.Api.Domain;

namespace Inventory.UnitTests.Domain;

public sealed class StockItemTests
{
    private static readonly DateTimeOffset _createdOn = new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _changedOn = new(2026, 9, 18, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Create_SkuInAnotherCase_StoresItNormalized()
    {
        var stockItem = StockItem.Create(" golden-lager-350 ", 100, _createdOn);

        stockItem.Sku.ShouldBe("GOLDEN-LAGER-350");
        stockItem.QuantityAvailable.ShouldBe(100);
        stockItem.QuantityReserved.ShouldBe(0);
        stockItem.UpdatedOnUtc.ShouldBe(_createdOn);
    }

    [Fact]
    public void Create_NegativeQuantity_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => StockItem.Create("GOLDEN-LAGER-350", -1, _createdOn));

    [Fact]
    public void Reserve_AvailableQuantity_MovesItToReserved()
    {
        var stockItem = StockItem.Create("GOLDEN-LAGER-350", 100, _createdOn);

        stockItem.Reserve(30, _changedOn);

        stockItem.QuantityAvailable.ShouldBe(70);
        stockItem.QuantityReserved.ShouldBe(30);
        stockItem.UpdatedOnUtc.ShouldBe(_changedOn);
    }

    [Fact]
    public void Reserve_MoreThanAvailable_Throws()
    {
        // The rule is enforced by StockReservation before anything is reserved, so reaching this is a bug.
        var stockItem = StockItem.Create("GOLDEN-LAGER-350", 10, _createdOn);

        Should.Throw<InvalidOperationException>(() => stockItem.Reserve(11, _changedOn));
    }

    [Fact]
    public void SetQuantityAvailable_Replenishment_KeepsReservedPacks()
    {
        var stockItem = StockItem.Create("GOLDEN-LAGER-350", 10, _createdOn);
        stockItem.Reserve(4, _createdOn);

        stockItem.SetQuantityAvailable(50, _changedOn);

        stockItem.QuantityAvailable.ShouldBe(50);
        stockItem.QuantityReserved.ShouldBe(4);
        stockItem.UpdatedOnUtc.ShouldBe(_changedOn);
    }

    [Fact]
    public void SetQuantityAvailable_NegativeQuantity_Throws()
    {
        var stockItem = StockItem.Create("GOLDEN-LAGER-350", 10, _createdOn);

        Should.Throw<ArgumentOutOfRangeException>(() => stockItem.SetQuantityAvailable(-1, _changedOn));
    }
}
