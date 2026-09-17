using BuildingBlocks.Common.Results;

namespace Ordering.Application.Abstractions.Catalog;

/// <summary>
/// Port to the Catalog service. The adapter (Infrastructure) owns HTTP, resilience and serialization;
/// when Catalog cannot be reached it returns <see cref="CatalogErrors.Unavailable"/> instead of throwing.
/// </summary>
public interface ICatalogClient
{
    /// <returns>The products that exist among <paramref name="skus"/>; unknown SKUs are simply absent.</returns>
    Task<Result<IReadOnlyList<CatalogProduct>>> GetProductsAsync(IReadOnlyCollection<string> skus, CancellationToken cancellationToken);
}

/// <summary>Price snapshot of a product. <see cref="Price"/> is per pack and has no currency (Catalog does not store one).</summary>
public sealed record CatalogProduct(string Sku, string Name, decimal Price);

public static class CatalogErrors
{
    public static readonly Error Unavailable =
        Error.Unavailable("Catalog.Unavailable", "The catalog is temporarily unavailable. Please try again in a few seconds.");
}
