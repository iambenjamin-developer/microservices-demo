namespace Gateway.Cors;

public static class CorsPolicies
{
    /// <summary>The policy the web app (and the YARP routes it calls) uses.</summary>
    public const string Web = "web";
}

/// <summary>Allowed browser origins (section <c>Cors</c>).</summary>
public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; set; } = [];
}

public static class CorsExtensions
{
    /// <summary>
    /// The browser is the only caller that needs CORS, so the allowed origins are configuration, never "*":
    /// the web app's origin locally, the deployed origin in production.
    /// </summary>
    public static IServiceCollection AddWebCors(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();

        return services.AddCors(cors => cors.AddPolicy(CorsPolicies.Web, policy => policy
            .WithOrigins(options.AllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            // The web app follows the Location header of a 202 Accepted to poll the new order.
            .WithExposedHeaders("Location")));
    }
}
