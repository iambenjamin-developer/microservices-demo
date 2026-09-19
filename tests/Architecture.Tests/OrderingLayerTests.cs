using System.Reflection;
using NetArchTest.Rules;
using Ordering.Application.Abstractions.Messaging;
using Ordering.Domain.Orders;
using Ordering.Infrastructure.Persistence;

namespace Architecture.Tests;

/// <summary>
/// Clean Architecture dependency rule for Ordering, checked on the compiled assemblies:
/// Domain ← Application ← Infrastructure ← Api. A wrong reference fails the build pipeline, not a code review.
/// </summary>
public sealed class OrderingLayerTests
{
    private static readonly Assembly _domain = typeof(Order).Assembly;
    private static readonly Assembly _application = typeof(ICommand<>).Assembly;
    private static readonly Assembly _infrastructure = typeof(OrderingDbContext).Assembly;
    private static readonly Assembly _api = typeof(Program).Assembly;

    private const string ApplicationNamespace = "Ordering.Application";
    private const string InfrastructureNamespace = "Ordering.Infrastructure";
    private const string ApiNamespace = "Ordering.Api";

    [Fact]
    public void Domain_DoesNotDependOnOuterLayers()
    {
        var result = Types.InAssembly(_domain)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace, ApiNamespace)
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Domain_DoesNotDependOnFrameworks()
    {
        var result = Types.InAssembly(_domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Microsoft.Extensions",
                "Azure",
                "BuildingBlocks.Messaging",
                "BuildingBlocks.Contracts",
                "FluentValidation",
                "Riok.Mapperly")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Application_DoesNotDependOnOuterLayers()
    {
        var result = Types.InAssembly(_application)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespace, ApiNamespace)
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Application_DoesNotDependOnInfrastructureFrameworks()
    {
        var result = Types.InAssembly(_application)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Azure",
                "Npgsql",
                "Polly",
                "System.Net.Http",
                "BuildingBlocks.Messaging")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Infrastructure_DoesNotDependOnApi()
    {
        var result = Types.InAssembly(_infrastructure)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void CommandHandlers_AreInternalAndSealed()
    {
        // Callers must go through ICommandHandler<,> (and therefore the decorators), never the concrete class.
        var handlers = Types.InAssembly(_application)
            .That()
            .ImplementInterface(typeof(ICommandHandler<,>))
            .And()
            .DoNotHaveNameEndingWith("Decorator`2");

        // Guard against a vacuous pass: the selection must actually find the handlers.
        handlers.GetTypes().Count().ShouldBeGreaterThanOrEqualTo(3);

        var result = handlers
            .Should()
            .NotBePublic()
            .And()
            .BeSealed()
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Endpoints_DoNotUseInfrastructureDirectly()
    {
        // Endpoints talk to the application ports; only Program.cs (the composition root) knows Infrastructure.
        var result = Types.InAssembly(_api)
            .That()
            .ResideInNamespace("Ordering.Api.Endpoints")
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespace, "Microsoft.EntityFrameworkCore")
            .GetResult();

        AssertSuccessful(result);
    }

    private static void AssertSuccessful(NetArchTest.Rules.TestResult result) =>
        result.IsSuccessful.ShouldBeTrue(
            $"Violating types: {string.Join(", ", result.FailingTypeNames ?? [])}");
}
