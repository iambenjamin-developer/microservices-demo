using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ordering.Infrastructure.Persistence;

public static class DatabaseMigrationExtensions
{
    /// <summary>
    /// Applies pending migrations at startup. Convenient for a single-instance demo; in production migrations
    /// run as a deployment step (EF migration bundle or SQL script) so replicas never race on schema changes.
    /// </summary>
    public static async Task MigrateOrderingDatabaseAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
