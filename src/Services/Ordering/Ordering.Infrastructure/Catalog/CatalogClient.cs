using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Common.Results;
using Microsoft.Extensions.Logging;
using Ordering.Application.Abstractions.Catalog;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Ordering.Infrastructure.Catalog;

/// <summary>
/// Adapter for <see cref="ICatalogClient"/>. Retries, timeouts and the circuit breaker run in the HttpClient
/// pipeline (see <see cref="CatalogClientRegistration"/>); this class only turns what is left after them into
/// <see cref="CatalogErrors.Unavailable"/>, so callers get a <see cref="Result"/> instead of an exception.
/// </summary>
internal sealed partial class CatalogClient(HttpClient httpClient, ILogger<CatalogClient> logger) : ICatalogClient
{
    public async Task<Result<IReadOnlyList<CatalogProduct>>> GetProductsAsync(
        IReadOnlyCollection<string> skus,
        CancellationToken cancellationToken)
    {
        if (skus.Count == 0)
        {
            return Array.Empty<CatalogProduct>();
        }

        // One call for every SKU of the order: GET /api/products?sku=A&sku=B
        var query = string.Join('&', skus.Select(sku => $"sku={Uri.EscapeDataString(sku)}"));

        try
        {
            var products = await httpClient.GetFromJsonAsync<List<CatalogProductDto>>($"api/products?{query}", cancellationToken);

            return products is null
                ? Array.Empty<CatalogProduct>()
                : products.Select(product => new CatalogProduct(product.Sku, product.Name, product.Price)).ToList();
        }
        catch (HttpRequestException exception) when (exception.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            // Not a transient fault: the relayed token was rejected. Logged apart so a misconfigured
            // signing key is not mistaken for a Catalog outage.
            LogCatalogRejectedTheCall(logger, exception, exception.StatusCode);
            return CatalogErrors.Unavailable;
        }
        catch (Exception exception) when (exception is HttpRequestException or TimeoutRejectedException or BrokenCircuitException or JsonException)
        {
            LogCatalogUnavailable(logger, exception, exception.GetType().Name);
            return CatalogErrors.Unavailable;
        }
    }

    /// <summary>Only the fields Ordering needs; the rest of Catalog's response is ignored (tolerant reader).</summary>
    private sealed record CatalogProductDto(string Sku, string Name, decimal Price);

    [LoggerMessage(Level = LogLevel.Error, Message = "Catalog rejected the call with {StatusCode}: the access token was not accepted")]
    private static partial void LogCatalogRejectedTheCall(ILogger logger, Exception exception, HttpStatusCode? statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Catalog call failed after the resilience pipeline ({ExceptionType})")]
    private static partial void LogCatalogUnavailable(ILogger logger, Exception exception, string exceptionType);
}
