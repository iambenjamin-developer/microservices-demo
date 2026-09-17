using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Ordering.Application.Abstractions.Catalog;
using Polly;

namespace Ordering.Infrastructure.Catalog;

internal static class CatalogClientRegistration
{
    public const string PipelineName = "catalog";

    /// <summary>
    /// Typed HttpClient with an explicit Polly v8 pipeline. Strategies run outermost first:
    /// <list type="number">
    /// <item>total timeout — caps the whole call, retries included, so a request never hangs;</item>
    /// <item>retry — exponential backoff with jitter on transient failures (5xx, 408, 429, network, attempt timeout);</item>
    /// <item>circuit breaker — after too many failures it opens and calls fail fast instead of piling up on a sick Catalog;</item>
    /// <item>attempt timeout — cancels a single slow attempt so the retry can try again.</item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddCatalogClient(this IServiceCollection services)
    {
        services.AddOptions<CatalogClientOptions>()
            .BindConfiguration(CatalogClientOptions.SectionName)
            .Validate(
                options => options.TotalTimeout > options.AttemptTimeout && options.AttemptTimeout > TimeSpan.Zero && options.MaxRetryAttempts >= 0,
                "Catalog: TotalTimeout must be greater than AttemptTimeout (> 0) and MaxRetryAttempts cannot be negative.")
            .ValidateOnStart();

        var httpClient = services.AddHttpClient<ICatalogClient, CatalogClient>((serviceProvider, client) =>
            client.BaseAddress = serviceProvider.GetRequiredService<IOptions<CatalogClientOptions>>().Value.BaseAddress);

        // ServiceDefaults adds the standard resilience handler to every client; remove it so handlers are never
        // stacked (nested retries multiply attempts) and this pipeline is the only one in force.
#pragma warning disable EXTEXP0001 // Experimental API, but the documented way to replace the default handler.
        httpClient.RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

        httpClient.AddResilienceHandler(PipelineName, static (pipeline, context) =>
        {
            var options = context.ServiceProvider.GetRequiredService<IOptions<CatalogClientOptions>>().Value;

            pipeline
                .AddTimeout(options.TotalTimeout)
                .AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = options.MaxRetryAttempts,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = options.RetryBaseDelay,
                })
                .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    MinimumThroughput = 5,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    BreakDuration = options.CircuitBreakDuration,
                })
                .AddTimeout(options.AttemptTimeout);
        });

        return services;
    }
}
