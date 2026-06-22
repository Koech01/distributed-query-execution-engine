using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DistributedQuery.Worker;

public static class WorkerHealthEndpoints
{
    public static IEndpointRouteBuilder MapWorkerHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health/live", (WorkerHealthService healthService) =>
        {
            return healthService.IsLive()
                ? Results.Ok(new { status = "live" })
                : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        });

        endpoints.MapGet("/health/ready", async (
            WorkerHealthService healthService,
            CancellationToken cancellationToken) =>
        {
            var ready = await healthService.IsReadyAsync(cancellationToken).ConfigureAwait(false);
            return ready
                ? Results.Ok(new { status = "ready" })
                : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        });

        return endpoints;
    }
}
