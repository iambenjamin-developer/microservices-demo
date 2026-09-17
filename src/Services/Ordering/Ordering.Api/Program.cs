using System.Text.Json.Serialization;
using BuildingBlocks.Web.Endpoints;
using BuildingBlocks.Web.Results;
using Ordering.Api.Customers;
using Ordering.Application;
using Ordering.Infrastructure;
using Ordering.Infrastructure.Persistence;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Composition root: each layer registers its own services; dependencies still point inward.
builder.Services.AddOrderingApplication();
builder.AddOrderingInfrastructure();

builder.Services.AddOptions<DemoCustomerOptions>().BindConfiguration(DemoCustomerOptions.SectionName);

builder.Services.AddApiProblemDetails();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

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

await app.Services.MigrateOrderingDatabaseAsync();
await app.RunAsync();
