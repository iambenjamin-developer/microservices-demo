using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Persistence;

internal static class DatabaseMigrationExtensions
{
    /// <summary>
    /// Applies pending migrations (and then the seeding configured on the context) at startup.
    /// Convenient for a single-instance demo; in production migrations run as a deployment step
    /// (EF migration bundle or SQL script) so replicas never race on schema changes.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
