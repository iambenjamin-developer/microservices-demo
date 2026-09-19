using BuildingBlocks.Web.Authentication;
using BuildingBlocks.Web.Mvc;
using BuildingBlocks.Web.OpenApi;
using BuildingBlocks.Web.Results;
using Ordering.Api.Customers;
using Ordering.Application;
using Ordering.Application.Abstractions.Security;
using Ordering.Infrastructure;
using Ordering.Infrastructure.Persistence;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Composition root: each layer registers its own services; dependencies still point inward.
builder.Services.AddOrderingApplication();
builder.AddOrderingInfrastructure();

builder.AddJwtAuthentication();

// Calls to Catalog go out on behalf of the point of sale being served, carrying its token.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAccessTokenProvider, HttpContextAccessTokenProvider>();

builder.Services.AddApiProblemDetails();
builder.Services.AddOpenApi(options => options.AddBearerSecurityScheme());

builder.Services.AddApiControllers();

var app = builder.Build();

app.UseApiExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapControllers();

await app.Services.MigrateOrderingDatabaseAsync();
await app.RunAsync();
