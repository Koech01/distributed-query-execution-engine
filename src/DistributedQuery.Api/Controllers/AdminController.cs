using System.Diagnostics;
using DistributedQuery.Api.Services;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DistributedQuery.Api.Controllers;

[ApiController]
[Route("admin")]
[Authorize(Policy = ApiServiceCollectionExtensions.QueryAdminPolicy)]
public sealed class AdminController : ControllerBase
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Api.AdminController");

    private readonly AdminDashboardService _dashboardService;
    private readonly IQueryCoordinatorClient _coordinatorClient;
    private readonly IQueryCacheAdmin _queryCacheAdmin;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        AdminDashboardService dashboardService,
        IQueryCoordinatorClient coordinatorClient,
        IQueryCacheAdmin queryCacheAdmin,
        ILogger<AdminController> logger)
    {
        _dashboardService = dashboardService ?? throw new ArgumentNullException(nameof(dashboardService));
        _coordinatorClient = coordinatorClient ?? throw new ArgumentNullException(nameof(coordinatorClient));
        _queryCacheAdmin = queryCacheAdmin ?? throw new ArgumentNullException(nameof(queryCacheAdmin));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet("stats")]
    [ProducesResponseType(typeof(AdminDashboardStats), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatsAsync(CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("api.admin.stats", ActivityKind.Server);
        var stats = await _dashboardService.GetStatsAsync(cancellationToken).ConfigureAwait(false);
        return Ok(stats);
    }

    [HttpGet("cache/stats")]
    [ProducesResponseType(typeof(AdminCacheStats), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCacheStatsAsync(CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("api.admin.cache.stats", ActivityKind.Server);
        var stats = await _queryCacheAdmin.GetStatsAsync(cancellationToken).ConfigureAwait(false);
        return Ok(stats);
    }

    [HttpPost("cache/flush")]
    [ProducesResponseType(typeof(AdminCacheFlushResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> FlushCacheAsync(
        [FromBody] AdminCacheFlushRequest? request,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("api.admin.cache.flush", ActivityKind.Server);

        try
        {
            var result = await _queryCacheAdmin
                .FlushPlansAsync(request?.PlanHash, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Admin flushed plan cache scope {Scope}, deleted {DeletedCount} entries",
                result.Scope,
                result.DeletedPlanEntries);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { type = "invalid_request", message = ex.Message });
        }
    }

    [HttpGet("queries/active")]
    [ProducesResponseType(typeof(ActiveQueryPage), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveQueriesAsync(
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("api.admin.queries.active", ActivityKind.Server);
        var page = await _coordinatorClient
            .GetActiveQueriesAsync(Math.Clamp(limit, 1, 200), Math.Max(0, offset), cancellationToken)
            .ConfigureAwait(false);
        return Ok(page);
    }

    [HttpPost("queries/{id:guid}/cancel")]
    [ProducesResponseType(typeof(CancelQueryResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelQueryAsync(Guid id, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("api.admin.queries.cancel", ActivityKind.Server);
        activity?.SetTag("query.id", id.ToString("D"));

        var result = await _coordinatorClient.CancelQueryAsync(id, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Admin cancel query {QueryId}: found={Found}, cancellationRequested={CancellationRequested}",
            id,
            result.Found,
            result.CancellationRequested);

        return Ok(result);
    }

    [HttpGet("workers")]
    [ProducesResponseType(typeof(WorkerHealthDashboard), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWorkersAsync(CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("api.admin.workers", ActivityKind.Server);
        var dashboard = await _coordinatorClient.GetWorkerHealthAsync(cancellationToken).ConfigureAwait(false);
        return Ok(dashboard);
    }
}
