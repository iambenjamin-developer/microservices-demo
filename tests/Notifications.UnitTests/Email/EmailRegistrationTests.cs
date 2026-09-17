using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Notifications.Email;

namespace Notifications.UnitTests.Email;

/// <summary>
/// The Null Object is chosen once, at composition time. These tests are what keeps that promise: if the
/// choice ever leaked into the handler as an <c>if</c>, turning e-mail off would stop being a configuration change.
/// </summary>
public sealed class EmailRegistrationTests
{
    [Fact]
    public void AddEmailSending_EmailDisabled_ResolvesTheNullObject()
    {
        using var host = BuildHost(("Email:Enabled", "false"));

        host.Services.GetRequiredService<IEmailSender>().ShouldBeOfType<NoOpEmailSender>();
    }

    [Fact]
    public void AddEmailSending_EmailEnabled_ResolvesTheSmtpSender()
    {
        using var host = BuildHost(("Email:Enabled", "true"));

        host.Services.GetRequiredService<IEmailSender>().ShouldBeOfType<SmtpEmailSender>();
    }

    [Fact]
    public void AddEmailSending_NoEmailSection_DefaultsToSendingThroughTheLocalMailCatcher()
    {
        using var host = BuildHost();

        var options = host.Services.GetRequiredService<IOptions<EmailOptions>>().Value;

        host.Services.GetRequiredService<IEmailSender>().ShouldBeOfType<SmtpEmailSender>();
        options.Port.ShouldBe(1025);
        options.UseStartTls.ShouldBeFalse();
    }

    [Fact]
    public void AddEmailSending_InvalidSenderAddress_FailsAtStartupNotAtSendTime()
    {
        using var host = BuildHost(("Email:From", "not-an-address"));

        Should.Throw<OptionsValidationException>(() => host.Services.GetRequiredService<IOptions<EmailOptions>>().Value);
    }

    [Fact]
    public async Task SendAsync_NullObject_ReportsThatNothingWasSent()
    {
        using var host = BuildHost(("Email:Enabled", "false"));
        var sender = host.Services.GetRequiredService<IEmailSender>();

        var delivery = await sender.SendAsync(new EmailMessage("bar@example.com", "Subject", "Body"), TestContext.Current.CancellationToken);

        delivery.ShouldBe(EmailDelivery.Skipped);
    }

    private static IHost BuildHost(params (string Key, string Value)[] settings)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)));
        builder.Services.AddLogging();
        builder.AddEmailSending();

        return builder.Build();
    }
}
