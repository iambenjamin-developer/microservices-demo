using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Messaging.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Messaging:Outbox";

    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

    public int BatchSize { get; set; } = 20;

    /// <summary>After this many failed attempts a message is left for manual inspection.</summary>
    public int MaxAttempts { get; set; } = 10;
}

/// <summary>
/// Polls the outbox table and publishes pending messages. <c>FOR UPDATE SKIP LOCKED</c> lets several
/// replicas of the same service run the processor concurrently without publishing a message twice.
/// Delivery is at-least-once: if the app crashes after sending but before committing, the message is resent,
/// which is why every consumer is idempotent.
/// </summary>
internal sealed partial class OutboxProcessor<TDbContext>(
    IServiceScopeFactory scopeFactory,
    IEventBus eventBus,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor<TDbContext>> logger) : BackgroundService
    where TDbContext : DbContext
{
    private readonly OutboxOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var published = 0;

            try
            {
                published = await PublishPendingAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogBatchFailed(logger, ex);
            }

            if (published < _options.BatchSize)
            {
                await Task.Delay(_options.PollingInterval, timeProvider, stoppingToken);
            }
        }
    }

    private async Task<int> PublishPendingAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

        // A retrying execution strategy (enabled by Aspire's Npgsql integration) rejects user-initiated
        // transactions unless the whole unit runs inside the strategy, so a transient failure retries all of it.
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            (Processor: this, DbContext: dbContext),
            static (_, state, cancellationToken) => state.Processor.PublishBatchAsync(state.DbContext, cancellationToken),
            verifySucceeded: null,
            cancellationToken);
    }

    private async Task<int> PublishBatchAsync(TDbContext dbContext, CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var messages = await dbContext.Set<OutboxMessage>()
            .FromSql($"""
                SELECT * FROM outbox_messages
                WHERE processed_on_utc IS NULL AND attempts < {_options.MaxAttempts}
                ORDER BY occurred_on_utc
                LIMIT {_options.BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                await eventBus.PublishAsync(
                    new OutgoingMessage(message.Id, message.Topic, message.Subject, message.Payload, message.CorrelationId, message.TraceParent),
                    cancellationToken);

                message.ProcessedOnUtc = timeProvider.GetUtcNow();
                LogPublished(logger, message.Subject, message.Id);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.Attempts++;
                message.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                LogPublishFailed(logger, ex, message.Subject, message.Id, message.Attempts);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return messages.Count;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Published {Subject} {MessageId} from outbox")]
    private static partial void LogPublished(ILogger logger, string subject, Guid messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to publish {Subject} {MessageId} (attempt {Attempts})")]
    private static partial void LogPublishFailed(ILogger logger, Exception exception, string subject, Guid messageId, int attempts);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox batch failed")]
    private static partial void LogBatchFailed(ILogger logger, Exception exception);
}
