using BuildingBlocks.Web.Endpoints;
using BuildingBlocks.Web.Results;
using FluentValidation;
using Inventory.Api.Features;
using Inventory.Api.Mapping;
using Inventory.Api.Messaging;
using Inventory.Api.Persistence;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<InventoryDbContext>(
    InventoryDbContext.ConnectionName,
    configureDbContextOptions: options => options.UseInventorySeeding());

builder.AddInventoryMessaging();

builder.Services.AddApiProblemDetails();
builder.Services.AddOpenApi();

builder.Services.AddValidatorsFromAssemblyContaining<Program>(includeInternalTypes: true);
builder.Services.AddInventoryMapping();
builder.Services.AddInventoryFeatures();
builder.Services.AddEndpoints(typeof(Program).Assembly);

var app = builder.Build();

app.UseApiExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapDefaultEndpoints();
app.MapEndpoints();

await app.MigrateDatabaseAsync();
await app.RunAsync();
