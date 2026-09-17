using BuildingBlocks.Messaging.Persistence;
using Inventory.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Persistence;

/// <summary>
/// The inventory database. A single <c>SaveChanges</c> commits the reserved stock, the outcome event in the
/// outbox and the inbox record of the message that caused them — the reservation cannot be lost or duplicated.
/// </summary>
public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    /// <summary>Aspire connection name (matches the database resource declared in the AppHost).</summary>
    public const string ConnectionName = "inventorydb";

    public DbSet<StockItem> StockItems => Set<StockItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InventoryDbContext).Assembly);
        modelBuilder.AddMessagingTables();
    }
}
