using Catalog.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Persistence;

/// <summary>
/// Fictional demo catalog, plugged into EF Core <c>UseSeeding</c>/<c>UseAsyncSeeding</c>:
/// it runs right after migrations are applied and only inserts into an empty table.
/// </summary>
/// <remarks>Inventory seeds its stock with these same SKUs.</remarks>
public static class CatalogSeeder
{
    public static DbContextOptionsBuilder UseCatalogSeeding(this DbContextOptionsBuilder options) =>
        options
            .UseSeeding((context, _) =>
            {
                var products = context.Set<Product>();
                if (!products.Any())
                {
                    products.AddRange(CreateProducts());
                    context.SaveChanges();
                }
            })
            .UseAsyncSeeding(async (context, _, cancellationToken) =>
            {
                var products = context.Set<Product>();
                if (!await products.AnyAsync(cancellationToken))
                {
                    products.AddRange(CreateProducts());
                    await context.SaveChangesAsync(cancellationToken);
                }
            });

    public static IReadOnlyList<Product> CreateProducts() =>
    [
        Product.Create("DUFF-STYLE-CLASSIC-350", "Duff-Style Classic Lager 350ml", BeerStyle.Lager, 350, 12, 14.40m),
        Product.Create("GOLDEN-LAGER-350", "Golden Lager 350ml", BeerStyle.Lager, 350, 12, 12.00m),
        Product.Create("GOLDEN-LAGER-1000", "Golden Lager 1L", BeerStyle.Lager, 1000, 6, 15.90m),
        Product.Create("CRISP-PILSNER-269", "Crisp Pilsner 269ml", BeerStyle.Pilsner, 269, 15, 11.25m),
        Product.Create("SUNNY-WHEAT-330", "Sunny Wheat 330ml", BeerStyle.Wheat, 330, 24, 31.20m),
        Product.Create("AMBER-ALE-600", "Amber Ale 600ml", BeerStyle.AmberAle, 600, 12, 27.60m),
        Product.Create("HOPPY-TRAIL-IPA-473", "Hoppy Trail IPA 473ml", BeerStyle.Ipa, 473, 6, 17.70m),
        Product.Create("MIDNIGHT-STOUT-500", "Midnight Stout 500ml", BeerStyle.Stout, 500, 6, 19.50m),
    ];
}
