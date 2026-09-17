using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ordering.Domain.Orders;
using Ordering.Domain.ValueObjects;

namespace Ordering.Infrastructure.Persistence.Configurations;

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public const int ProductNameMaxLength = 100;

    public void Configure(EntityTypeBuilder<OrderItem> item)
    {
        item.ToTable("order_items");
        item.HasKey(i => i.Id);

        item.Property(i => i.Id).HasColumnName("id").ValueGeneratedNever();
        item.Property<Guid>("OrderId").HasColumnName("order_id");
        item.Property(i => i.ProductName).HasColumnName("product_name").HasMaxLength(ProductNameMaxLength);

        // Single-value objects are complex types too (not value converters) so a projection like Sku.Value
        // translates to the column instead of forcing client evaluation.
        item.ComplexProperty(i => i.Sku, sku => sku.Property(s => s.Value).HasColumnName("sku").HasMaxLength(Sku.MaxLength));
        item.ComplexProperty(i => i.Quantity, quantity => quantity.Property(q => q.Value).HasColumnName("quantity"));
        item.ComplexProperty(i => i.UnitPrice, money => money.MapMoney("unit_price"));

        // Computed from UnitPrice and Quantity; storing it would duplicate data that can go stale.
        item.Ignore(i => i.LineTotal);

        item.HasIndex("OrderId").HasDatabaseName("ix_order_items_order_id");
    }
}
