using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Notifications.Persistence;

/// <summary>
/// Used only by <c>dotnet ef</c>. The real connection string is injected by Aspire at runtime,
/// so migrations can be generated without starting the AppHost (no connection is opened).
/// </summary>
internal sealed class NotificationsDbContextDesignTimeFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseNpgsql("Host=localhost;Database=notificationsdb")
            .Options);
}
