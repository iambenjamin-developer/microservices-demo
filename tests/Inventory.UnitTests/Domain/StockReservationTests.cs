using Inventory.Api.Domain;

namespace Inventory.UnitTests.Domain;

public sealed class StockReservationTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Reserve_EveryLineAvailable_MovesQuantitiesToReserved()
    {
        var lager = StockItem.Create("GOLDEN-LAGER-350", 100, _now);
        var ale = StockItem.Create("AMBER-ALE-600", 50, _now);

        var outcome = StockReservation.Reserve(
            [lager, ale],
            [new ReservationLine("GOLDEN-LAGER-350", 10), new ReservationLine("AMBER-ALE-600", 5)],
            _now);

        outcome.IsReserved.ShouldBeTrue();
        outcome.Reason.ShouldBeNull();
        outcome.UnavailableSkus.ShouldBeEmpty();
        lager.QuantityAvailable.ShouldBe(90);
        lager.QuantityReserved.ShouldBe(10);
        ale.QuantityAvailable.ShouldBe(45);
        ale.QuantityReserved.ShouldBe(5);
    }

    [Fact]
    public void Reserve_QuantityEqualToAvailable_Reserves()
    {
        var stout = StockItem.Create("MIDNIGHT-STOUT-500", 5, _now);

        var outcome = StockReservation.Reserve([stout], [new ReservationLine("MIDNIGHT-STOUT-500", 5)], _now);

        outcome.IsReserved.ShouldBeTrue();
        stout.QuantityAvailable.ShouldBe(0);
        stout.QuantityReserved.ShouldBe(5);
    }

    [Fact]
    public void Reserve_OneLineShort_ReservesNothing()
    {
        // All or nothing: the line that could be served must stay untouched.
        var lager = StockItem.Create("GOLDEN-LAGER-350", 100, _now);
        var stout = StockItem.Create("MIDNIGHT-STOUT-500", 5, _now);

        var outcome = StockReservation.Reserve(
            [lager, stout],
            [new ReservationLine("GOLDEN-LAGER-350", 10), new ReservationLine("MIDNIGHT-STOUT-500", 6)],
            _now);

        outcome.IsReserved.ShouldBeFalse();
        outcome.Reason.ShouldBe(StockReservation.InsufficientStockReason);
        outcome.UnavailableSkus.ShouldBe(["MIDNIGHT-STOUT-500"]);
        lager.QuantityAvailable.ShouldBe(100);
        lager.QuantityReserved.ShouldBe(0);
        stout.QuantityAvailable.ShouldBe(5);
    }

    [Fact]
    public void Reserve_UnknownSku_RejectsWithoutTouchingTheOtherItems()
    {
        var lager = StockItem.Create("GOLDEN-LAGER-350", 100, _now);

        var outcome = StockReservation.Reserve(
            [lager],
            [new ReservationLine("GOLDEN-LAGER-350", 10), new ReservationLine("NOT-STOCKED-001", 1)],
            _now);

        outcome.IsReserved.ShouldBeFalse();
        outcome.Reason.ShouldBe(StockReservation.UnknownSkuReason);
        outcome.UnavailableSkus.ShouldBe(["NOT-STOCKED-001"]);
        lager.QuantityAvailable.ShouldBe(100);
    }

    [Fact]
    public void Reserve_UnknownAndShortSkus_ReportsBothSortedAndACombinedReason()
    {
        var stout = StockItem.Create("MIDNIGHT-STOUT-500", 1, _now);

        var outcome = StockReservation.Reserve(
            [stout],
            [new ReservationLine("MIDNIGHT-STOUT-500", 2), new ReservationLine("NOT-STOCKED-001", 1)],
            _now);

        outcome.IsReserved.ShouldBeFalse();
        outcome.Reason.ShouldBe(StockReservation.UnknownSkuAndInsufficientStockReason);
        outcome.UnavailableSkus.ShouldBe(["MIDNIGHT-STOUT-500", "NOT-STOCKED-001"]);
    }

    [Fact]
    public void Reserve_RepeatedSku_AddsTheQuantitiesUp()
    {
        var stout = StockItem.Create("MIDNIGHT-STOUT-500", 10, _now);

        var outcome = StockReservation.Reserve(
            [stout],
            [new ReservationLine("MIDNIGHT-STOUT-500", 6), new ReservationLine("MIDNIGHT-STOUT-500", 6)],
            _now);

        outcome.IsReserved.ShouldBeFalse();
        outcome.UnavailableSkus.ShouldBe(["MIDNIGHT-STOUT-500"]);
        stout.QuantityAvailable.ShouldBe(10);
    }

    [Fact]
    public void Reserve_SkuInAnotherCase_MatchesTheStoredSku()
    {
        var lager = StockItem.Create("GOLDEN-LAGER-350", 100, _now);

        var outcome = StockReservation.Reserve([lager], [new ReservationLine(" golden-lager-350 ", 10)], _now);

        outcome.IsReserved.ShouldBeTrue();
        lager.QuantityReserved.ShouldBe(10);
    }

    [Fact]
    public void Reserve_NoLines_Throws() =>
        Should.Throw<ArgumentException>(() => StockReservation.Reserve([], [], _now));

    [Fact]
    public void Reserve_LineWithoutQuantity_Throws()
    {
        var lager = StockItem.Create("GOLDEN-LAGER-350", 100, _now);

        Should.Throw<ArgumentOutOfRangeException>(
            () => StockReservation.Reserve([lager], [new ReservationLine("GOLDEN-LAGER-350", 0)], _now));
    }
}
