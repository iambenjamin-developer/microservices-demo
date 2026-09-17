namespace Inventory.Api.Domain;

/// <summary>
/// The stock of one SKU, counted in packs — the same unit Catalog prices and Ordering buys.
/// Reserving moves packs from <see cref="QuantityAvailable"/> to <see cref="QuantityReserved"/>, so the
/// two columns together always show what was promised to orders and what is still sellable.
/// </summary>
/// <remarks>
/// The SKU is the cross-service identity: Inventory never reads the Catalog database, it only knows SKUs.
/// Business rules decide <i>whether</i> a reservation is possible (see <see cref="StockReservation"/>);
/// the methods here are guarded so a rule that was not checked fails loudly instead of corrupting the row.
/// </remarks>
public sealed class StockItem
{
    public const int SkuMaxLength = 50;

    private StockItem()
    {
    }

    public Guid Id { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public int QuantityAvailable { get; private set; }

    public int QuantityReserved { get; private set; }

    public DateTimeOffset UpdatedOnUtc { get; private set; }

    public static StockItem Create(string sku, int quantityAvailable, DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentOutOfRangeException.ThrowIfNegative(quantityAvailable);

        return new StockItem
        {
            Id = Guid.CreateVersion7(),
            Sku = NormalizeSku(sku),
            QuantityAvailable = quantityAvailable,
            UpdatedOnUtc = utcNow,
        };
    }

    public bool CanReserve(int quantity) => quantity <= QuantityAvailable;

    /// <summary>Reserves packs for an order. Callers must check <see cref="CanReserve"/> first.</summary>
    public void Reserve(int quantity, DateTimeOffset utcNow)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        if (!CanReserve(quantity))
        {
            throw new InvalidOperationException(
                $"Cannot reserve {quantity} packs of '{Sku}': only {QuantityAvailable} are available.");
        }

        QuantityAvailable -= quantity;
        QuantityReserved += quantity;
        UpdatedOnUtc = utcNow;
    }

    /// <summary>Replenishment by an operator. Reserved packs are not touched: they belong to orders already placed.</summary>
    public void SetQuantityAvailable(int quantityAvailable, DateTimeOffset utcNow)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(quantityAvailable);

        QuantityAvailable = quantityAvailable;
        UpdatedOnUtc = utcNow;
    }

    public static string NormalizeSku(string sku) => sku.Trim().ToUpperInvariant();
}
