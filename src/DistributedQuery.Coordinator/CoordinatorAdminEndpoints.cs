using DistributedQuery.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace DistributedQuery.Coordinator;

public static class CoordinatorAdminEndpoints
{
    public static IEndpointRouteBuilder MapCoordinatorAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/internal/v1/admin/dashboard", GetDashboardAsync);
        endpoints.MapGet("/internal/v1/admin/queries/active", GetActiveQueriesAsync);
        endpoints.MapPost("/internal/v1/admin/queries/{id:guid}/cancel", CancelQueryAsync);
        endpoints.MapGet("/internal/v1/admin/workers", GetWorkerHealthAsync);
        return endpoints;
    }

    private static Task<IResult> GetDashboardAsync(
        CoordinatorAdminService adminService,
        CancellationToken cancellationToken) =>
        ExecuteAsync(adminService.GetDashboardStatsAsync(cancellationToken));

    private static Task<IResult> GetActiveQueriesAsync(
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CoordinatorAdminService adminService,
        CancellationToken cancellationToken) =>
        ExecuteAsync(adminService.GetActiveQueriesAsync(limit ?? 50, offset ?? 0, cancellationToken));

    private static Task<IResult> CancelQueryAsync(
        Guid id,
        CoordinatorAdminService adminService,
        CancellationToken cancellationToken) =>
        ExecuteAsync(adminService.CancelQueryAsync(id, cancellationToken));

    private static Task<IResult> GetWorkerHealthAsync(
        CoordinatorAdminService adminService,
        CancellationToken cancellationToken) =>
        ExecuteAsync(adminService.GetWorkerHealthAsync(cancellationToken));

    private static async Task<IResult> ExecuteAsync<T>(Task<T> operation)
    {
        var result = await operation.ConfigureAwait(false);
        return Results.Json(result, CoordinatorQueryEndpoints.JsonOptions);
    }
}
