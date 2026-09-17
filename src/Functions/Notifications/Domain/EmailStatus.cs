namespace Notifications.Domain;

/// <summary>
/// What happened to the e-mail that accompanies a notification. The notification itself is always stored:
/// e-mail is a second channel, and a mail server being down must not lose the record the panel shows.
/// </summary>
public enum EmailStatus
{
    /// <summary>Stored but not attempted yet — the value a row keeps if the process dies before delivery.</summary>
    Pending,

    /// <summary>E-mail delivery is turned off (<c>Email:Enabled = false</c>).</summary>
    Skipped,

    Sent,

    /// <summary>The SMTP server refused or could not be reached; the notification is still readable in the panel.</summary>
    Failed,
}
