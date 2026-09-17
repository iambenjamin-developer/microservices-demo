using Microsoft.Extensions.Logging;

namespace Notifications.Email;

/// <summary>
/// Null Object: what <see cref="IEmailSender"/> resolves to when <c>Email:Enabled</c> is <c>false</c>.
/// The notification is still stored and still shows in the panel — only the second channel is off.
/// </summary>
internal sealed partial class NoOpEmailSender(ILogger<NoOpEmailSender> logger) : IEmailSender
{
    public Task<EmailDelivery> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        LogSkipped(logger, message.To, message.Subject);
        return Task.FromResult(EmailDelivery.Skipped);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "E-mail delivery is disabled; skipped \"{Subject}\" to {To}")]
    private static partial void LogSkipped(ILogger logger, string to, string subject);
}
