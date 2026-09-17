using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Notifications.Persistence;

internal static class DatabaseMigrationExtensions
{
    /// <summary>
    /// Applies pending migrations before the Functions host starts listening, so the first message
    /// never arrives at a missing table. Convenient for a single-instance demo; in production
    /// migrations run as a deployment step so replicas never race on schema changes.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this IHost host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
