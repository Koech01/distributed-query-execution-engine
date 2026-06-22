using System.Threading.Channels;
using DistributedQuery.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DistributedQuery.Coordinator;

public sealed class QueryBackgroundDispatcher
{
    private readonly Channel<QueryRequest> _channel = Channel.CreateBounded<QueryRequest>(
        new BoundedChannelOptions(1_000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask EnqueueAsync(QueryRequest request, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(request, cancellationToken);

    internal ChannelReader<QueryRequest> Reader => _channel.Reader;
}

public sealed class QueryBackgroundProcessor : BackgroundService
{
    private readonly QueryBackgroundDispatcher _dispatcher;
    private readonly CoordinatorService _coordinatorService;
    private readonly ILogger<QueryBackgroundProcessor> _logger;

    public QueryBackgroundProcessor(
        QueryBackgroundDispatcher dispatcher,
        CoordinatorService coordinatorService,
        ILogger<QueryBackgroundProcessor> logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _coordinatorService = coordinatorService ?? throw new ArgumentNullException(nameof(coordinatorService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in _dispatcher.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await _coordinatorService.ExecuteQueryAsync(request, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background query {QueryId} failed", request.QueryId);
            }
        }
    }
}
