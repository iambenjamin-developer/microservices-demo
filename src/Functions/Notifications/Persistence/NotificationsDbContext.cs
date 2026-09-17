using BuildingBlocks.Messaging.Persistence;
using Microsoft.EntityFrameworkCore;
using Notifications.Domain;

namespace Notifications.Persistence;

/// <summary>
/// The notifications database. Only the inbox table is added, not the outbox: this service is the end of the
/// choreography — it consumes order outcomes and publishes nothing back.
/// </summary>
public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options)
{
    /// <summary>Aspire connection name (matches the database resource declared in the AppHost).</summary>
    public const string ConnectionName = "notificationsdb";

    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
        modelBuilder.AddInboxTable();
    }
}
