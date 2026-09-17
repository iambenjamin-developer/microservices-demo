using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Catalog.Api.Persistence;

/// <summary>
/// Used only by <c>dotnet ef</c>. The real connection string is injected by Aspire at runtime,
/// so migrations can be generated without starting the AppHost (no connection is opened).
/// </summary>
internal sealed class CatalogDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql("Host=localhost;Database=catalogdb")
            .Options);
}
