using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ordering.Domain.Orders;

namespace Ordering.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public const int CustomerIdMaxLength = 100;
    public const int CustomerEmailMaxLength = 254;

    public void Configure(EntityTypeBuilder<Order> order)
    {
        order.ToTable("orders");
        order.HasKey(o => o.Id);

        order.Property(o => o.Id).HasColumnName("id").ValueGeneratedNever();
        order.Property(o => o.CustomerId).HasColumnName("customer_id").HasMaxLength(CustomerIdMaxLength);
        order.Property(o => o.CustomerEmail).HasColumnName("customer_email").HasMaxLength(CustomerEmailMaxLength);
        order.Property(o => o.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        order.Property(o => o.RejectionReason).HasColumnName("rejection_reason");
        order.Property(o => o.PlacedOnUtc).HasColumnName("placed_on_utc");
        order.Property(o => o.CompletedOnUtc).HasColumnName("completed_on_utc");

        // Value objects as complex types: columns live in the orders table, no joins, and queries can still
        // project members such as Total.Amount to SQL.
        order.ComplexProperty(o => o.Subtotal, money => money.MapMoney("subtotal"));
        order.ComplexProperty(o => o.Discount, money => money.MapMoney("discount"));
        order.ComplexProperty(o => o.Total, money => money.MapMoney("total"));

        // Optimistic concurrency on PostgreSQL's xmin system column: if two messages change the same order at
        // once, the second SaveChanges fails and the consumer retries against the fresh state.
        order.Property<uint>("Version").IsRowVersion();

        // Items are only reachable through the aggregate root; EF writes the private _items field directly.
        order.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey("OrderId")
            .OnDelete(DeleteBehavior.Cascade);
        order.Navigation(o => o.Items).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Domain events are transient: they are turned into outbox rows when saving, never stored as such.
        order.Ignore(o => o.DomainEvents);

        // "My orders, most recent first" is the main read path.
        order.HasIndex(o => new { o.CustomerId, o.PlacedOnUtc })
            .HasDatabaseName("ix_orders_customer_id_placed_on_utc")
            .IsDescending(false, true);
    }
}
