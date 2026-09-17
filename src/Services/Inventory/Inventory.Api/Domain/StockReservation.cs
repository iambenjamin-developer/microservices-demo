namespace Inventory.Api.Domain;

/// <summary>
/// The reservation rule of the service, written as a pure function so it can be unit tested without a database:
/// a reservation is <b>all or nothing</b>. Every line is checked first and only then is anything reserved, so a
/// partially filled order never happens and the caller gets the full list of SKUs that blocked it.
/// </summary>
public static class StockReservation
{
    public const string UnknownSkuReason = "Some products are not stocked.";
    public const string InsufficientStockReason = "Not enough stock for some products.";
    public const string UnknownSkuAndInsufficientStockReason = "Some products are not stocked and others are out of stock.";

    /// <remarks>
    /// <paramref name="stockItems"/> holds the rows found for the requested SKUs; a SKU with no row is reported
    /// as unknown. Repeated SKUs in <paramref name="lines"/> are added up before the check.
    /// </remarks>
    public static ReservationOutcome Reserve(
        IReadOnlyCollection<StockItem> stockItems,
        IReadOnlyCollection<ReservationLine> lines,
        DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(stockItems);
        ArgumentNullException.ThrowIfNull(lines);

        if (lines.Count == 0)
        {
            throw new ArgumentException("A reservation must have at least one line.", nameof(lines));
        }

        var itemsBySku = stockItems.ToDictionary(item => item.Sku, StringComparer.Ordinal);
        var requestedBySku = lines
            .GroupBy(line => StockItem.NormalizeSku(line.Sku), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity), StringComparer.Ordinal);

        List<string> unknownSkus = [];
        List<string> insufficientSkus = [];

        foreach (var (sku, quantity) in requestedBySku)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity, nameof(lines));

            if (!itemsBySku.TryGetValue(sku, out var item))
            {
                unknownSkus.Add(sku);
            }
            else if (!item.CanReserve(quantity))
            {
                insufficientSkus.Add(sku);
            }
        }

        if (unknownSkus.Count > 0 || insufficientSkus.Count > 0)
        {
            var reason = (unknownSkus.Count, insufficientSkus.Count) switch
            {
                (> 0, 0) => UnknownSkuReason,
                (0, > 0) => InsufficientStockReason,
                _ => UnknownSkuAndInsufficientStockReason,
            };

            // Sorted so the same shortage always produces the same message, whatever order the rows came back in.
            return ReservationOutcome.Rejected(reason, [.. unknownSkus.Concat(insufficientSkus).Order(StringComparer.Ordinal)]);
        }

        foreach (var (sku, quantity) in requestedBySku)
        {
            itemsBySku[sku].Reserve(quantity, utcNow);
        }

        return ReservationOutcome.Reserved();
    }
}
