using DistributedQuery.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DistributedQuery.Api.Controllers;

[ApiController]
[Route("health")]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    private readonly ApiHealthService _healthService;

    public HealthController(ApiHealthService healthService) =>
        _healthService = healthService ?? throw new ArgumentNullException(nameof(healthService));

    [HttpGet("live")]
    public IActionResult GetLive() =>
        _healthService.IsLive()
            ? Ok(new { status = "live" })
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "not_live" });

    [HttpGet("ready")]
    public async Task<IActionResult> GetReadyAsync(CancellationToken cancellationToken)
    {
        var ready = await _healthService.IsReadyAsync(cancellationToken).ConfigureAwait(false);
        return ready
            ? Ok(new { status = "ready" })
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "not_ready" });
    }
}
