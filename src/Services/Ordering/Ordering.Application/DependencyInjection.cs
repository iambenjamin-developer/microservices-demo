using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ordering.Application.Abstractions.Messaging;
using Ordering.Application.Decorators;
using Ordering.Application.Orders;
using Ordering.Application.Orders.ConfirmOrder;
using Ordering.Application.Orders.PlaceOrder;
using Ordering.Application.Orders.RejectOrder;
using Ordering.Application.Pricing;
using Ordering.Domain.Discounts;
using Ordering.Domain.Orders;
using Ordering.Domain.ValueObjects;

namespace Ordering.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddOrderingApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);
        services.AddPricing();

        // One line per use case: the list doubles as the index of what the service can do.
        services.AddCommandHandler<PlaceOrderCommand, OrderResponse, PlaceOrderCommandHandler>();
        services.AddCommandHandler<ConfirmOrderCommand, OrderStatus, ConfirmOrderCommandHandler>();
        services.AddCommandHandler<RejectOrderCommand, OrderStatus, RejectOrderCommandHandler>();

        return services;
    }

    private static void AddPricing(this IServiceCollection services)
    {
        services.AddOptions<PricingOptions>()
            .Validate(options => Money.Create(0, options.Currency).IsSuccess, "Pricing:Currency must be an ISO 4217 code.")
            .ValidateOnStart();

        // Strategy selected from configuration; the constructor of VolumeDiscountPolicy rejects invalid tiers.
        services.AddSingleton<IDiscountPolicy>(serviceProvider =>
        {
            var tiers = serviceProvider.GetRequiredService<IOptions<PricingOptions>>().Value.VolumeDiscountTiers;
            return tiers.Count == 0 ? NoDiscountPolicy.Instance : new VolumeDiscountPolicy(tiers);
        });
    }

    /// <summary>
    /// Registers <typeparamref name="THandler"/> wrapped by the decorators, outermost first:
    /// logging → validation → handler. Callers resolve <see cref="ICommandHandler{TCommand, TResponse}"/>.
    /// </summary>
    private static void AddCommandHandler<TCommand, TResponse, THandler>(this IServiceCollection services)
        where TCommand : ICommand<TResponse>
        where THandler : class, ICommandHandler<TCommand, TResponse>
    {
        services.AddScoped<THandler>();
        services.AddScoped<ICommandHandler<TCommand, TResponse>>(serviceProvider =>
            new LoggingDecorator<TCommand, TResponse>(
                new ValidationDecorator<TCommand, TResponse>(
                    serviceProvider.GetRequiredService<THandler>(),
                    serviceProvider.GetServices<IValidator<TCommand>>()),
                serviceProvider.GetRequiredService<ILogger<LoggingDecorator<TCommand, TResponse>>>()));
    }
}
