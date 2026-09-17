using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Inventory.Api.Persistence;

/// <summary>
/// Used only by <c>dotnet ef</c>. The real connection string is injected by Aspire at runtime,
/// so migrations can be generated without starting the AppHost (no connection is opened).
/// </summary>
internal sealed class InventoryDbContextDesignTimeFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql("Host=localhost;Database=inventorydb")
            .Options);
}
