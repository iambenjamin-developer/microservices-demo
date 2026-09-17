using BuildingBlocks.Common.Results;
using BuildingBlocks.Messaging.Consumers;
using Microsoft.Extensions.Logging;

namespace Ordering.Infrastructure.Messaging;

/// <summary>What a consumer does with a failed command result.</summary>
internal static partial class IntegrationEventFailure
{
    /// <summary>
    /// A <see cref="ErrorType.Conflict"/> (the order already left <c>Pending</c>) will never succeed on retry, so the
    /// message is completed and the fact is logged. Anything else (e.g. order not found) throws: the message is
    /// abandoned, retried and, after the subscription's max delivery count, dead-lettered for inspection.
    /// </summary>
    public static void CompleteOrThrow(ILogger logger, Error error, string eventName, Guid orderId, IntegrationEventContext context)
    {
        if (error.Type == ErrorType.Conflict)
        {
            LogIgnored(logger, eventName, orderId, error.Description, context.MessageId);
            return;
        }

        throw new InvalidOperationException(
            $"{eventName} for order '{orderId}' could not be applied ({error.Code}): {error.Description}");
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Ignored {EventName} for order {OrderId}: {Reason} (message {MessageId})")]
    private static partial void LogIgnored(ILogger logger, string eventName, Guid orderId, string reason, Guid messageId);
}
