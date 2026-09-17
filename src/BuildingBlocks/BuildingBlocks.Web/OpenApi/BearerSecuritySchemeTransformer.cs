using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace BuildingBlocks.Web.OpenApi;

/// <summary>
/// Declares the bearer scheme in the OpenAPI document and applies it to every operation that is not
/// anonymous, so the API reference UI offers an "Authorize" box and sends the token.
/// </summary>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    private const string SchemeId = "Bearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeId] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Access token issued by the Gateway (POST /auth/token).",
        };

        // Document-wide requirement: every operation needs a token unless it opts out.
        document.Security =
        [
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(SchemeId, document)] = [],
            },
        ];

        return Task.CompletedTask;
    }
}

public static class OpenApiSecurityExtensions
{
    /// <summary>Adds the bearer security scheme to the generated OpenAPI document.</summary>
    public static OpenApiOptions AddBearerSecurityScheme(this OpenApiOptions options) =>
        options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
}
