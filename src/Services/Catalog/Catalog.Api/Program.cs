using System.Text.Json.Serialization;
using BuildingBlocks.Web.Endpoints;
using BuildingBlocks.Web.Results;
using Catalog.Api.Features;
using Catalog.Api.Persistence;
using FluentValidation;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<CatalogDbContext>(
    CatalogDbContext.ConnectionName,
    configureDbContextOptions: options => options.UseCatalogSeeding());

builder.Services.AddApiProblemDetails();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddValidatorsFromAssemblyContaining<Program>(includeInternalTypes: true);
builder.Services.AddCatalogFeatures();
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
