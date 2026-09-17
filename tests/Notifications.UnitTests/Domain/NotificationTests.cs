using Notifications.Domain;

namespace Notifications.UnitTests.Domain;

public sealed class NotificationTests
{
    private static readonly DateTimeOffset _storedOn = new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _sentOn = new(2026, 9, 17, 12, 0, 1, TimeSpan.Zero);

    [Fact]
    public void Create_NewNotification_StartsWithoutAnEmailAttempt()
    {
        var notification = CreateNotification();

        notification.EmailStatus.ShouldBe(EmailStatus.Pending);
        notification.EmailSentOnUtc.ShouldBeNull();
        notification.EmailError.ShouldBeNull();
    }

    [Fact]
    public void MarkEmailSent_DeliveredEmail_RecordsWhen()
    {
        var notification = CreateNotification();

        notification.MarkEmailSent(_sentOn);

        notification.EmailStatus.ShouldBe(EmailStatus.Sent);
        notification.EmailSentOnUtc.ShouldBe(_sentOn);
    }

    [Fact]
    public void MarkEmailSkipped_DeliveryTurnedOff_IsNotAFailure()
    {
        var notification = CreateNotification();

        notification.MarkEmailSkipped();

        notification.EmailStatus.ShouldBe(EmailStatus.Skipped);
        notification.EmailError.ShouldBeNull();
    }

    [Fact]
    public void MarkEmailFailed_SmtpError_KeepsTheReasonWithoutLosingTheNotification()
    {
        var notification = CreateNotification();

        notification.MarkEmailFailed("Connection refused.");

        notification.EmailStatus.ShouldBe(EmailStatus.Failed);
        notification.EmailSentOnUtc.ShouldBeNull();
        notification.EmailError.ShouldBe("Connection refused.");
        notification.Title.ShouldNotBeEmpty();
    }

    [Fact]
    public void Create_BodyLongerThanTheColumn_IsTruncatedInsteadOfFailingTheSave()
    {
        var notification = Notification.Create(
            Guid.CreateVersion7(),
            "bar",
            "bar@example.com",
            NotificationType.OrderRejected,
            "Order rejected",
            new string('x', Notification.BodyMaxLength + 100),
            _storedOn,
            _storedOn);

        notification.Body.Length.ShouldBe(Notification.BodyMaxLength);
    }

    private static Notification CreateNotification() =>
        Notification.Create(
            Guid.CreateVersion7(),
            "bar",
            "bar@example.com",
            NotificationType.OrderConfirmed,
            "Order #ABCDEF12 confirmed",
            "Your order was confirmed.",
            _storedOn,
            _storedOn);
}
