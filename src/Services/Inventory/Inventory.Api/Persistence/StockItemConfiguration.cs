using Inventory.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Api.Persistence;

internal sealed class StockItemConfiguration : IEntityTypeConfiguration<StockItem>
{
    public void Configure(EntityTypeBuilder<StockItem> stockItem)
    {
        stockItem.ToTable(
            "stock_items",
            table => table.HasCheckConstraint(
                "ck_stock_items_quantities",
                "quantity_available >= 0 AND quantity_reserved >= 0"));

        stockItem.HasKey(s => s.Id);

        stockItem.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        stockItem.Property(s => s.Sku).HasColumnName("sku").HasMaxLength(StockItem.SkuMaxLength);
        stockItem.Property(s => s.QuantityAvailable).HasColumnName("quantity_available");
        stockItem.Property(s => s.QuantityReserved).HasColumnName("quantity_reserved");
        stockItem.Property(s => s.UpdatedOnUtc).HasColumnName("updated_on_utc");

        // Optimistic concurrency on PostgreSQL's xmin system column. Two OrderPlaced messages competing for the
        // same SKU cannot both reserve the last packs: the second SaveChanges fails and the message is retried
        // against fresh quantities (lost update prevented without locking the row for the whole handler).
        stockItem.Property<uint>("Version").IsRowVersion();

        // One row per SKU: the unique index is what makes "the stock of a SKU" a fact and not a sum of rows.
        stockItem.HasIndex(s => s.Sku).IsUnique().HasDatabaseName("ix_stock_items_sku");
    }
}
