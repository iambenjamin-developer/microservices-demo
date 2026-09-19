using BuildingBlocks.Common.Results;
using BuildingBlocks.Web.Mvc;
using BuildingBlocks.Web.Results;
using Inventory.Api.Contracts;
using Inventory.Api.Controllers;
using Inventory.Api.Domain;
using Inventory.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Inventory.UnitTests.Controllers;

/// <summary>
/// The controller only translates HTTP to <see cref="IStockService"/> and back, so the service is the one thing
/// mocked here. Stock queries and saves are covered against a real PostgreSQL in <c>Inventory.IntegrationTests</c>.
/// </summary>
public sealed class StockControllerTests
{
    private static readonly StockResponse _goldenLager =
        new("GOLDEN-LAGER-350", 70, 30, 100, new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero));

    private readonly IStockService _stockService = Substitute.For<IStockService>();

    [Fact]
    public async Task GetStock_SkuFilter_PassesSkusToServiceAndReturnsOk()
    {
        string[] skus = ["golden-lager-350"];
        _stockService.GetStockAsync(skus, Arg.Any<CancellationToken>()).Returns([_goldenLager]);

        var result = await CreateController().GetStock(new GetStockRequest(skus), TestContext.Current.CancellationToken);

        var ok = result.Result.ShouldBeOfType<OkObjectResult>();
        ok.Value.ShouldBe(new[] { _goldenLager });
        await _stockService.Received(1).GetStockAsync(skus, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetStock_NoFilter_PassesNullToService()
    {
        _stockService.GetStockAsync(null, Arg.Any<CancellationToken>()).Returns([]);

        var result = await CreateController().GetStock(new GetStockRequest(null), TestContext.Current.CancellationToken);

        result.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(Array.Empty<StockResponse>());
        await _stockService.Received(1).GetStockAsync(null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateStock_ServiceSucceeds_ReturnsOkWithUpdatedStock()
    {
        _stockService.UpdateQuantityAvailableAsync("golden-lager-350", new UpdateStockRequest(70), Arg.Any<CancellationToken>())
            .Returns(Result.Success(_goldenLager));

        var result = await CreateController().UpdateStock(
            "golden-lager-350", new UpdateStockRequest(70), TestContext.Current.CancellationToken);

        result.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(_goldenLager);
        await _stockService.Received(1).UpdateQuantityAvailableAsync("golden-lager-350", new UpdateStockRequest(70), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateStock_UnknownSku_Returns404ProblemWithErrorCode()
    {
        _stockService.UpdateQuantityAvailableAsync("NOT-STOCKED", new UpdateStockRequest(5), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<StockResponse>(StockErrors.NotFound("NOT-STOCKED")));

        var result = await CreateController().UpdateStock("NOT-STOCKED", new UpdateStockRequest(5), TestContext.Current.CancellationToken);

        var objectResult = result.Result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        var problem = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Status.ShouldBe(StatusCodes.Status404NotFound);
        problem.Detail.ShouldBe("No stock is kept for SKU 'NOT-STOCKED'.");
        problem.Extensions["code"].ShouldBe("Stock.NotFound");
    }

    /// <summary>
    /// A failed result is written through MVC's <c>ProblemDetailsFactory</c>, so the controller needs an
    /// <see cref="HttpContext"/> whose services hold the same problem details setup as the running service.
    /// </summary>
    private StockController CreateController()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddApiProblemDetails();
        services.AddApiControllers();

        return new StockController(_stockService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() },
            },
        };
    }
}
