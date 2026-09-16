using System.Diagnostics;

namespace BuildingBlocks.Messaging.Diagnostics;

/// <summary>
/// Custom tracing for the outbox and consumers. The W3C <c>traceparent</c> travels in the outbox row and in the
/// message application properties, so one distributed trace spans HTTP request → outbox → broker → consumer.
/// </summary>
public static class MessagingDiagnostics
{
    public const string ActivitySourceName = "BuildingBlocks.Messaging";
    public const string TraceParentProperty = "traceparent";

    private static readonly ActivitySource _activitySource = new(ActivitySourceName);

    public static Activity? StartPublish(string topic, string subject, string? parentTraceId) =>
        Start($"publish {topic}", ActivityKind.Producer, parentTraceId, subject);

    public static Activity? StartProcess(string subscription, string subject, string? parentTraceId) =>
        Start($"process {subscription}", ActivityKind.Consumer, parentTraceId, subject);

    private static Activity? Start(string name, ActivityKind kind, string? parentTraceId, string subject)
    {
        ActivityContext.TryParse(parentTraceId, null, out var parentContext);

        var activity = _activitySource.StartActivity(name, kind, parentContext);
        activity?.SetTag("messaging.system", "servicebus");
        activity?.SetTag("messaging.message.subject", subject);
        return activity;
    }
}
