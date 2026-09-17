using Catalog.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Api.Persistence;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> product)
    {
        product.ToTable("products");
        product.HasKey(p => p.Id);

        product.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        product.Property(p => p.Sku).HasColumnName("sku").HasMaxLength(Product.SkuMaxLength);
        product.Property(p => p.Name).HasColumnName("name").HasMaxLength(Product.NameMaxLength);
        product.Property(p => p.Style).HasColumnName("style").HasConversion<string>().HasMaxLength(30);
        product.Property(p => p.VolumeMl).HasColumnName("volume_ml");
        product.Property(p => p.PackSize).HasColumnName("pack_size");
        product.Property(p => p.Price).HasColumnName("price").HasPrecision(Product.PricePrecision, Product.PriceScale);

        // The SKU is the business key shared with other services; the unique index is the last line of defense.
        product.HasIndex(p => p.Sku).IsUnique().HasDatabaseName("ix_products_sku");
    }
}
