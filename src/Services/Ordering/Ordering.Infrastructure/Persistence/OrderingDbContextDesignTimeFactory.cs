using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ordering.Infrastructure.Persistence;

/// <summary>
/// Used only by <c>dotnet ef</c>. The real connection string is injected by Aspire at runtime,
/// so migrations can be generated without starting the AppHost (no connection is opened).
/// </summary>
internal sealed class OrderingDbContextDesignTimeFactory : IDesignTimeDbContextFactory<OrderingDbContext>
{
    public OrderingDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<OrderingDbContext>()
            .UseNpgsql("Host=localhost;Database=orderingdb")
            .Options);
}
