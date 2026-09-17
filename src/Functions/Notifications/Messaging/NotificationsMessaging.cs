using BuildingBlocks.Messaging.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Notifications.Messaging;

internal static class NotificationsMessaging
{
    /// <summary>
    /// Everything this service needs to consume order outcomes. It deliberately does <b>not</b> call
    /// <c>AddServiceBusMessaging()</c>: the Functions host creates and owns the Service Bus connection through
    /// the trigger binding, and Notifications publishes nothing, so there is no event bus and no outbox here.
    /// What is reused from the building blocks is what crosses the boundary anyway — the event contracts, the
    /// wire format and the inbox table.
    /// </summary>
    public static IHostApplicationBuilder AddNotificationsMessaging(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddScoped<OrderEventHandler>();

        // The traceparent that travelled in the message is replayed on this source, so the Aspire dashboard
        // shows one trace from the HTTP request that placed the order down to the e-mail.
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddSource(MessagingDiagnostics.ActivitySourceName));

        return builder;
    }
}
