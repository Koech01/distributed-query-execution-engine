using System.Diagnostics;
using DistributedQuery.Core.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace DistributedQuery.Api.Services;

public sealed class ApiHealthService
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Api.ApiHealthService");

    private readonly IConnectionMultiplexer? _redis;
    private readonly IQueryCoordinatorClient _coordinatorClient;
    private readonly CoordinatorClientHealthProbe _coordinatorProbe;
    private readonly ILogger<ApiHealthService> _logger;

    public ApiHealthService(
        IQueryCoordinatorClient coordinatorClient,
        CoordinatorClientHealthProbe coordinatorProbe,
        ILogger<ApiHealthService> logger,
        IConnectionMultiplexer? redis = null)
    {
        _coordinatorClient = coordinatorClient ?? throw new ArgumentNullException(nameof(coordinatorClient));
        _coordinatorProbe = coordinatorProbe ?? throw new ArgumentNullException(nameof(coordinatorProbe));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _redis = redis;
    }

    public bool IsLive() => true;

    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("ApiHealthService.Readiness", ActivityKind.Internal);

        if (_redis is not null)
        {
            try
            {
                _ = await _redis.GetDatabase().PingAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LogWarning(ex, "Redis readiness check failed");
                return false;
            }
        }

        try
        {
            var coordinatorReady = await _coordinatorProbe.CheckAsync(cancellationToken).ConfigureAwait(false);
            if (!coordinatorReady)
            {
                _logger.LogWarning("Coordinator readiness check failed");
                return false;
            }
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogWarning(ex, "Coordinator readiness check failed");
            return false;
        }

        _ = _coordinatorClient;
        return true;
    }
}

public sealed class CoordinatorClientHealthProbe
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CoordinatorClientHealthProbe> _logger;

    public CoordinatorClientHealthProbe(HttpClient httpClient, ILogger<CoordinatorClientHealthProbe> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/health/live", cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Coordinator health probe request failed");
            return false;
        }
    }
}
