using System.Threading.RateLimiting;
using BuildingBlocks.Web.Authentication;

namespace Gateway.RateLimiting;

public static class RateLimitPolicies
{
    /// <summary>Applied to the proxied <c>/api/*</c> routes.</summary>
    public const string Api = "api";

    /// <summary>Applied to <c>/auth/token</c>: it is anonymous and accepts passwords.</summary>
    public const string Auth = "auth";
}

/// <summary>Fixed-window limits per policy (section <c>RateLimiting</c>).</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public RateLimitWindow Api { get; set; } = new() { PermitLimit = 100, Window = TimeSpan.FromSeconds(10) };

    public RateLimitWindow Auth { get; set; } = new() { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) };
}

public sealed class RateLimitWindow
{
    public int PermitLimit { get; set; }

    public TimeSpan Window { get; set; }
}

public static class RateLimitingExtensions
{
    /// <summary>
    /// Protects the services behind the Gateway from a single noisy client. Requests are counted per
    /// authenticated user when there is one and per remote IP otherwise, so one point of sale cannot
    /// spend everybody else's budget.
    /// </summary>
    public static IServiceCollection AddGatewayRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>()
            ?? new RateLimitingOptions();

        return services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(RateLimitPolicies.Api, context => FixedWindow(context, options.Api));
            limiter.AddPolicy(RateLimitPolicies.Auth, context => FixedWindow(context, options.Auth));
        });
    }

    private static RateLimitPartition<string> FixedWindow(HttpContext context, RateLimitWindow window) =>
        RateLimitPartition.GetFixedWindowLimiter(
            PartitionKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = window.PermitLimit,
                Window = window.Window,
                QueueLimit = 0,
            });

    private static string PartitionKey(HttpContext context) =>
        context.User.FindFirst(JwtClaimNames.Subject)?.Value
        ?? context.Connection.RemoteIpAddress?.ToString()
        ?? "unknown";
}
