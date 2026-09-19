using System.Text.Json.Serialization;
using BuildingBlocks.Web.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Web.Mvc;

public static class ApiControllerExtensions
{
    /// <summary>
    /// Registers MVC controllers configured to keep the HTTP contract of the minimal API services:
    /// enums as strings, FluentValidation before every action, and request validation owned by the validators.
    /// </summary>
    /// <remarks>
    /// Controllers do not read <c>ConfigureHttpJsonOptions</c> (that is the minimal API serializer), so the enum
    /// converter is configured here for MVC. <c>SuppressImplicitRequiredAttributeForNonNullableReferenceTypes</c>
    /// stops MVC from rejecting a missing non-nullable string on its own: the validators (and in Ordering the
    /// application layer) decide what is required and answer with their own error codes.
    /// </remarks>
    public static IMvcBuilder AddApiControllers(this IServiceCollection services) =>
        services
            .AddControllers(options =>
            {
                options.Filters.Add<ValidationActionFilter>();
                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            })
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
}
