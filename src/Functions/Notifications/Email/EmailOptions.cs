using System.ComponentModel.DataAnnotations;

namespace Notifications.Email;

/// <summary>
/// SMTP settings (section <c>Email</c>). The defaults point at Mailpit, the local mail catcher Aspire starts,
/// so the demo sends real SMTP traffic and shows the message in a UI without anybody owning a mail account.
/// Pointing <see cref="Host"/> at <c>smtp.gmail.com</c> with an App Password delivers for real.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>When <c>false</c> the Null Object sender is registered and nothing leaves the process.</summary>
    public bool Enabled { get; set; } = true;

    [Required(AllowEmptyStrings = false)]
    public string Host { get; set; } = "localhost";

    [Range(1, 65535)]
    public int Port { get; set; } = 1025;

    /// <summary>Mailpit accepts plain SMTP; Gmail needs STARTTLS on port 587.</summary>
    public bool UseStartTls { get; set; }

    public string? Username { get; set; }

    /// <summary>A real secret when Gmail is used: it comes from user-secrets or the environment, never from a file in git.</summary>
    public string? Password { get; set; }

    [Required(AllowEmptyStrings = false)]
    [EmailAddress]
    public string From { get; set; } = "no-reply@microservices-demo.local";

    public string FromDisplayName { get; set; } = "Beer Ordering Demo";

    /// <summary>A slow mail server must not hold a message lock open; delivery is best effort anyway.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}
