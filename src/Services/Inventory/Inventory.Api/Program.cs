using BuildingBlocks.Web.Authentication;
using BuildingBlocks.Web.Endpoints;
using BuildingBlocks.Web.OpenApi;
using BuildingBlocks.Web.Results;
using FluentValidation;
using Inventory.Api.Features;
using Inventory.Api.Messaging;
using Inventory.Api.Persistence;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<InventoryDbContext>(
    InventoryDbContext.ConnectionName,
    configureDbContextOptions: options => options.UseInventorySeeding());

builder.AddInventoryMessaging();

builder.AddJwtAuthentication();

builder.Services.AddApiProblemDetails();
builder.Services.AddOpenApi(options => options.AddBearerSecurityScheme());

builder.Services.AddValidatorsFromAssemblyContaining<Program>(includeInternalTypes: true);
builder.Services.AddInventoryFeatures();
builder.Services.AddEndpoints(typeof(Program).Assembly);

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
app.MapEndpoints();

await app.MigrateDatabaseAsync();
await app.RunAsync();
