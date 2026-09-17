using BuildingBlocks.Common.Results;
using Ordering.Application.Abstractions.Catalog;

namespace Ordering.IntegrationTests.Infrastructure;

/// <summary>
/// In-memory Catalog with the seeded prices. Requesting <see cref="UnavailableTriggerSku"/> simulates Catalog being
/// down (what the real client returns once the resilience pipeline gives up), so tests stay independent.
/// </summary>
public sealed class FakeCatalogClient : ICatalogClient
{
    public const string UnavailableTriggerSku = "CATALOG-DOWN";

    private static readonly Dictionary<string, CatalogProduct> _products = new[]
    {
        new CatalogProduct("GOLDEN-LAGER-350", "Golden Lager 350ml", 12.00m),
        new CatalogProduct("AMBER-ALE-600", "Amber Ale 600ml", 27.60m),
        new CatalogProduct("SUNNY-WHEAT-330", "Sunny Wheat 330ml", 31.20m),
    }.ToDictionary(product => product.Sku, StringComparer.Ordinal);

    public Task<Result<IReadOnlyList<CatalogProduct>>> GetProductsAsync(IReadOnlyCollection<string> skus, CancellationToken cancellationToken)
    {
        if (skus.Contains(UnavailableTriggerSku))
        {
            return Task.FromResult(Result.Failure<IReadOnlyList<CatalogProduct>>(CatalogErrors.Unavailable));
        }

        IReadOnlyList<CatalogProduct> found = [.. skus.Where(_products.ContainsKey).Select(sku => _products[sku])];
        return Task.FromResult(Result.Success(found));
    }
}
