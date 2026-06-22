using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DistributedQuery.Coordinator;

public static class CoordinatorHealthEndpoints
{
    public static IEndpointRouteBuilder MapCoordinatorHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
        endpoints.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }));
        return endpoints;
    }
}
