using System.Text.Json.Serialization;
using BuildingBlocks.Web.Authentication;
using BuildingBlocks.Web.Results;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Notifications.Api;
using Notifications.Authentication;
using Notifications.Email;
using Notifications.Messaging;
using Notifications.Persistence;

var builder = FunctionsApplication.CreateBuilder(args);

// ASP.NET Core integration: HTTP triggers run on a real HttpContext, so the same ProblemDetails,
// JSON and authentication pieces the other services use work here as well.
builder.ConfigureFunctionsWebApplication();

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<NotificationsDbContext>(NotificationsDbContext.ConnectionName);

builder.AddJwtAuthentication();
builder.AddEmailSending();
builder.AddNotificationsMessaging();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<GetNotificationsQuery>();

builder.Services.AddApiProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// The Functions pipeline has no UseAuthentication; this middleware plays that role for HTTP triggers.
builder.UseMiddleware<JwtAuthenticationMiddleware>();

var host = builder.Build();

await host.MigrateDatabaseAsync();
await host.RunAsync();
