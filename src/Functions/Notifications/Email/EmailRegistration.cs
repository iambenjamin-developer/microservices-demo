using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Notifications.Email;

public static class EmailRegistration
{
    /// <summary>
    /// Picks the sender once, at startup, from <c>Email:Enabled</c>. The choice between a real SMTP client
    /// and the Null Object is a composition decision, so no code downstream ever tests whether mail is on.
    /// </summary>
    public static IHostApplicationBuilder AddEmailSending(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var section = builder.Configuration.GetSection(EmailOptions.SectionName);

        builder.Services.AddOptions<EmailOptions>()
            .Bind(section)
            .ValidateDataAnnotations()
            .Validate(options => options.Timeout > TimeSpan.Zero, "Email:Timeout must be positive.")
            .ValidateOnStart();

        if (section.GetValue("Enabled", defaultValue: true))
        {
            builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            builder.Services.AddSingleton<IEmailSender, NoOpEmailSender>();
        }

        return builder;
    }
}
