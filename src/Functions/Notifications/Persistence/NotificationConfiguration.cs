using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Persistence;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> notification)
    {
        notification.ToTable("notifications");

        notification.HasKey(n => n.Id);

        notification.Property(n => n.Id).HasColumnName("id").ValueGeneratedNever();
        notification.Property(n => n.OrderId).HasColumnName("order_id");
        notification.Property(n => n.CustomerId).HasColumnName("customer_id").HasMaxLength(Notification.CustomerIdMaxLength);
        notification.Property(n => n.CustomerEmail).HasColumnName("customer_email").HasMaxLength(Notification.EmailMaxLength);
        notification.Property(n => n.Title).HasColumnName("title").HasMaxLength(Notification.TitleMaxLength);
        notification.Property(n => n.Body).HasColumnName("body").HasMaxLength(Notification.BodyMaxLength);
        notification.Property(n => n.OccurredOnUtc).HasColumnName("occurred_on_utc");
        notification.Property(n => n.CreatedOnUtc).HasColumnName("created_on_utc");
        notification.Property(n => n.EmailSentOnUtc).HasColumnName("email_sent_on_utc");
        notification.Property(n => n.EmailError).HasColumnName("email_error").HasMaxLength(Notification.BodyMaxLength);

        // Stored as text, not as an ordinal: adding a notification type later must not renumber the old rows.
        notification.Property(n => n.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(50);
        notification.Property(n => n.EmailStatus).HasColumnName("email_status").HasConversion<string>().HasMaxLength(20);

        // The panel always asks for "my notifications, newest first"; the index answers exactly that query.
        notification.HasIndex(n => new { n.CustomerId, n.CreatedOnUtc })
            .IsDescending(false, true)
            .HasDatabaseName("ix_notifications_customer_created");
    }
}
