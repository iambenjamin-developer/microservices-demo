using BuildingBlocks.Web.Authentication;
using BuildingBlocks.Web.Endpoints;
using BuildingBlocks.Web.OpenApi;
using BuildingBlocks.Web.Results;
using FluentValidation;
using Gateway.Authentication;
using Gateway.Cors;
using Gateway.RateLimiting;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Cross-cutting concerns live here so the services behind the Gateway do not each reimplement them.
builder.AddJwtAuthentication();
builder.Services.AddWebCors(builder.Configuration);
builder.Services.AddGatewayRateLimiting(builder.Configuration);

builder.Services.AddOptions<DemoUsersOptions>()
    .Configure<IConfiguration>((options, configuration) =>
        configuration.GetSection(DemoUsersOptions.SectionName).Bind(options.Accounts))
    .Validate(options => options.Accounts.Count > 0, "At least one demo account must be configured.")
    .ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<DemoTokenIssuer>();

// Routes, clusters and per-route policies come from configuration; destinations are Aspire service
// discovery names ("https+http://catalog"), resolved by the service discovery destination resolver.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

builder.Services.AddApiProblemDetails();
builder.Services.AddOpenApi(options => options.AddBearerSecurityScheme());

builder.Services.AddValidatorsFromAssemblyContaining<TokenEndpoint>(includeInternalTypes: true);
builder.Services.AddEndpoints(typeof(TokenEndpoint).Assembly);

var app = builder.Build();

app.UseApiExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

// Order matters: CORS answers the browser's preflight before authorization can reject it for having no token.
app.UseCors(CorsPolicies.Web);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapDefaultEndpoints();
app.MapEndpoints();
app.MapReverseProxy();

await app.RunAsync();
