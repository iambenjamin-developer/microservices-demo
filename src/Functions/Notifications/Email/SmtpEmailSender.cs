using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;

namespace Notifications.Email;

/// <summary>
/// MailKit over SMTP. A connection is opened per message: this service sends a handful of e-mails per order,
/// so a pooled connection would only add a failure mode (a socket idling until the server drops it).
/// </summary>
internal sealed partial class SmtpEmailSender(
    IOptions<EmailOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task<EmailDelivery> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var mail = new MimeMessage
        {
            Subject = message.Subject,
            Body = new TextPart(TextFormat.Plain) { Text = message.Body },
        };

        mail.From.Add(new MailboxAddress(_options.FromDisplayName, _options.From));
        mail.To.Add(MailboxAddress.Parse(message.To));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);

        using var client = new SmtpClient();

        var secureSocketOptions = _options.UseStartTls
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.Auto;

        await client.ConnectAsync(_options.Host, _options.Port, secureSocketOptions, timeout.Token);

        // Mailpit accepts anonymous SMTP; Gmail needs the address and an App Password.
        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            await client.AuthenticateAsync(_options.Username, _options.Password ?? string.Empty, timeout.Token);
        }

        await client.SendAsync(mail, timeout.Token);
        await client.DisconnectAsync(quit: true, timeout.Token);

        LogSent(logger, message.To, message.Subject);
        return EmailDelivery.Sent;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sent \"{Subject}\" to {To}")]
    private static partial void LogSent(ILogger logger, string to, string subject);
}
