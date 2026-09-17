namespace Notifications.Email;

/// <summary>
/// Adapter over the mail transport. The handler only knows this port, so Mailpit, Gmail or "nothing at all"
/// are a registration detail rather than an <c>if</c> repeated at every call site.
/// </summary>
/// <remarks>
/// The port returns what it did instead of just <c>Task</c>: the Null Object implementation is allowed to do
/// nothing, but it is not allowed to let the stored notification claim an e-mail was sent.
/// </remarks>
public interface IEmailSender
{
    Task<EmailDelivery> SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// An outgoing e-mail. <c>To</c> is the address carried by the order events, which comes from the
/// <c>email</c> claim of the token the point of sale signed in with.
/// </summary>
public sealed record EmailMessage(string To, string Subject, string Body);

public enum EmailDelivery
{
    /// <summary>Handed to the SMTP server, which accepted it.</summary>
    Sent,

    /// <summary>Nothing was sent because e-mail delivery is turned off.</summary>
    Skipped,
}
