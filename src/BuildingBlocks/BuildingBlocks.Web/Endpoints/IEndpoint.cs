using Microsoft.AspNetCore.Routing;

namespace BuildingBlocks.Web.Endpoints;

/// <summary>
/// A vertical slice exposes its HTTP surface by implementing this interface.
/// Endpoints are discovered once at startup, so adding a feature never touches <c>Program.cs</c>.
/// </summary>
public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}
