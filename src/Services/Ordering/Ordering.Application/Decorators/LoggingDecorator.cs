using System.Diagnostics;
using BuildingBlocks.Common.Results;
using Microsoft.Extensions.Logging;
using Ordering.Application.Abstractions.Messaging;

namespace Ordering.Application.Decorators;

/// <summary>
/// Decorator: outermost wrapper of every command handler. Logs the outcome (including validation failures
/// produced by inner decorators) and the elapsed time, so handlers contain no logging boilerplate.
/// </summary>
internal sealed partial class LoggingDecorator<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    ILogger<LoggingDecorator<TCommand, TResponse>> logger) : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken)
    {
        var commandName = typeof(TCommand).Name;
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var result = await inner.HandleAsync(command, cancellationToken);
            var elapsedMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

            if (result.IsSuccess)
            {
                LogSucceeded(logger, commandName, elapsedMs);
            }
            else
            {
                LogFailed(logger, commandName, result.Error.Code, result.Error.Description, elapsedMs);
            }

            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogThrew(logger, exception, commandName, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Command} succeeded in {ElapsedMs:0.0} ms")]
    private static partial void LogSucceeded(ILogger logger, string command, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Command} failed with {ErrorCode}: {ErrorDescription} ({ElapsedMs:0.0} ms)")]
    private static partial void LogFailed(ILogger logger, string command, string errorCode, string errorDescription, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Command} threw after {ElapsedMs:0.0} ms")]
    private static partial void LogThrew(ILogger logger, Exception exception, string command, double elapsedMs);
}
