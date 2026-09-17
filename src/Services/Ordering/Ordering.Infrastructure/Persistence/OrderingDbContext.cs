using BuildingBlocks.Messaging.Persistence;
using Microsoft.EntityFrameworkCore;
using Ordering.Application.Abstractions.Persistence;
using Ordering.Domain.Orders;

namespace Ordering.Infrastructure.Persistence;

/// <summary>
/// Unit of Work for the ordering database. <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> commits the
/// aggregates and, through <see cref="Outbox.DomainEventsToOutboxInterceptor"/>, their outbox messages atomically.
/// </summary>
public sealed class OrderingDbContext(DbContextOptions<OrderingDbContext> options) : DbContext(options), IUnitOfWork
{
    /// <summary>Aspire connection name (matches the database resource declared in the AppHost).</summary>
    public const string ConnectionName = "orderingdb";

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderingDbContext).Assembly);
        modelBuilder.AddMessagingTables();
    }
}
