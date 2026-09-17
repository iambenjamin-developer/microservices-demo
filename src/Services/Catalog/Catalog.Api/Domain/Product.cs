namespace Catalog.Api.Domain;

/// <summary>
/// A sellable beer presentation. Prices are per pack (B2B customers buy cases, not bottles).
/// The SKU is the identity shared with other services (Inventory, Ordering) and never changes.
/// </summary>
public sealed class Product
{
    public const int SkuMaxLength = 50;
    public const int NameMaxLength = 100;
    public const int PricePrecision = 10;
    public const int PriceScale = 2;

    private Product()
    {
    }

    public Guid Id { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public BeerStyle Style { get; private set; }

    public int VolumeMl { get; private set; }

    public int PackSize { get; private set; }

    public decimal Price { get; private set; }

    public static Product Create(string sku, string name, BeerStyle style, int volumeMl, int packSize, decimal price)
    {
        var product = new Product
        {
            Id = Guid.CreateVersion7(),
            Sku = NormalizeSku(sku),
        };

        product.Update(name, style, volumeMl, packSize, price);
        return product;
    }

    public void Update(string name, BeerStyle style, int volumeMl, int packSize, decimal price)
    {
        Name = name.Trim();
        Style = style;
        VolumeMl = volumeMl;
        PackSize = packSize;
        Price = price;
    }

    public static string NormalizeSku(string sku) => sku.Trim().ToUpperInvariant();
}
