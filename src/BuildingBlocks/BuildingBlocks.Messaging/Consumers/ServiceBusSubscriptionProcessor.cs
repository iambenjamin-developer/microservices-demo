using System.Data.Common;
using Azure.Messaging.ServiceBus;
using BuildingBlocks.Contracts;
using BuildingBlocks.Messaging.Diagnostics;
using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Messaging.Consumers;

/// <summary>
/// Receives messages from one topic subscription and dispatches them to the registered handlers.
/// Pipeline per message: inbox check → handler → save business change + inbox record atomically → complete.
/// Handler exceptions abandon the message; after the subscription's max delivery count the broker dead-letters it.
/// </summary>
internal sealed partial class ServiceBusSubscriptionProcessor<TDbContext>(
    string topic,
    string subscription,
    ServiceBusClient client,
    IntegrationEventHandlerRegistry registry,
    IServiceScopeFactory scopeFactory,
    IOptions<ConsumerOptions> options,
    TimeProvider timeProvider,
    ILogger<ServiceBusSubscriptionProcessor<TDbContext>> logger) : BackgroundService
    where TDbContext : DbContext
{
    private const string PostgresUniqueViolation = "23505";

    private ServiceBusProcessor? _processor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            LogDisabled(logger, subscription);
            return;
        }

        _processor = client.CreateProcessor(topic, subscription, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 4,
        });

        _processor.ProcessMessageAsync += ProcessMessageAsync;
        _processor.ProcessErrorAsync += ProcessErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor is not null)
        {
            await _processor.StopProcessingAsync(cancellationToken);
            await _processor.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        var message = args.Message;
        var cancellationToken = args.CancellationToken;

        message.ApplicationProperties.TryGetValue(MessagingDiagnostics.TraceParentProperty, out var traceParent);
        using var activity = MessagingDiagnostics.StartProcess(subscription, message.Subject, traceParent as string);

        // Defensive: subscription filters should prevent this, but an unknown event must never block the queue.
        if (!registry.TryGet(message.Subject, out var registration))
        {
            LogUnknownSubject(logger, message.Subject, subscription);
            await args.CompleteMessageAsync(message, cancellationToken);
            return;
        }

        if (!Guid.TryParse(message.MessageId, out var messageId) ||
            IntegrationEventSerializer.Deserialize(message.Body, registration.EventType) is not IntegrationEvent integrationEvent)
        {
            await args.DeadLetterMessageAsync(message, "InvalidMessage", "MessageId or payload could not be read.", cancellationToken);
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

        var alreadyProcessed = await dbContext.Set<InboxMessage>()
            .AnyAsync(m => m.MessageId == messageId && m.Consumer == subscription, cancellationToken);

        if (alreadyProcessed)
        {
            LogDuplicate(logger, message.Subject, messageId);
            await args.CompleteMessageAsync(message, cancellationToken);
            return;
        }

        var context = new IntegrationEventContext(messageId, message.CorrelationId, message.DeliveryCount);
        await registration.InvokeAsync(scope.ServiceProvider, integrationEvent, context, cancellationToken);

        dbContext.Set<InboxMessage>().Add(new InboxMessage
        {
            MessageId = messageId,
            Consumer = subscription,
            ProcessedOnUtc = timeProvider.GetUtcNow(),
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is DbException { SqlState: PostgresUniqueViolation })
        {
            // A concurrent delivery of the same message may have won the race and committed first.
            // Any other unique violation is a real error: rethrow so the message is retried.
            if (!await WasProcessedAsync(messageId, cancellationToken))
            {
                throw;
            }

            LogDuplicate(logger, message.Subject, messageId);
        }

        await args.CompleteMessageAsync(message, cancellationToken);
        LogProcessed(logger, message.Subject, messageId, subscription);
    }

    private async Task<bool> WasProcessedAsync(Guid messageId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TDbContext>()
            .Set<InboxMessage>()
            .AnyAsync(m => m.MessageId == messageId && m.Consumer == subscription, cancellationToken);
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        LogProcessorError(logger, args.Exception, args.ErrorSource.ToString(), args.EntityPath);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Processed {Subject} {MessageId} on {Subscription}")]
    private static partial void LogProcessed(ILogger logger, string subject, Guid messageId, string subscription);

    [LoggerMessage(Level = LogLevel.Information, Message = "Skipped duplicate {Subject} {MessageId}")]
    private static partial void LogDuplicate(ILogger logger, string subject, Guid messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No handler for subject {Subject} on {Subscription}; message completed")]
    private static partial void LogUnknownSubject(ILogger logger, string subject, string subscription);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Consumers are disabled; subscription {Subscription} is not processed")]
    private static partial void LogDisabled(ILogger logger, string subscription);

    [LoggerMessage(Level = LogLevel.Error, Message = "Service Bus processor error ({ErrorSource}) on {EntityPath}")]
    private static partial void LogProcessorError(ILogger logger, Exception exception, string errorSource, string entityPath);
}
