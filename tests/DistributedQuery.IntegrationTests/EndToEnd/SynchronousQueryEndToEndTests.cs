using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using DistributedQuery.Api;
using DistributedQuery.Api.Contracts;
using DistributedQuery.Coordinator;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using DistributedQuery.Infrastructure.Grpc;
using DistributedQuery.QueryParser.Parsing;
using DistributedQuery.Worker;
using DistributedQuery.Worker.Services;
using FluentAssertions;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SQLitePCL;
using CoreColumnDescriptor = DistributedQuery.Core.Models.ColumnDescriptor;

namespace DistributedQuery.IntegrationTests.EndToEnd;

public sealed class SynchronousQueryEndToEndTests
{
    private const string SelectOrdersSql = "SELECT id, amount FROM orders";

    [Fact]
    public async Task PostQueries_ReturnsMergedResultFromWorkerShards()
    {
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        await using var shard0 = await SqliteShardDatabase.CreateAsync("e2e-shard-0");
        await using var shard1 = await SqliteShardDatabase.CreateAsync("e2e-shard-1");
        await SeedOrdersAsync(shard0, (1, 10), (2, 20));
        await SeedOrdersAsync(shard1, (3, 30), (4, 40));

        var workerPort = Random.Shared.Next(10000, 60000);
        await using var worker = await WorkerHost.StartAsync(workerPort, shard0.ConnectionString, shard1.ConnectionString);

        var cache = new InMemoryQueryCache();
        using var workerClient = new WorkerGrpcClient(
            NullLogger<WorkerGrpcClient>.Instance,
            new GrpcChannelOptions(),
            null);
        var nodes = new StaticNodeRegistry(
        [
            new NodeInfo("worker-0", "127.0.0.1", workerPort, [0], "test"),
            new NodeInfo("worker-1", "127.0.0.1", workerPort, [1], "test")
        ]);

        await using var factory = new QueryApiFactory(cache, nodes, workerClient);
        using var client = factory.CreateClient();

        var queryId = Guid.NewGuid();
        var response = await client.PostAsJsonAsync("/queries", new
        {
            queryId,
            sql = SelectOrdersSql
        });

        var responseBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, responseBody);
        var result = await response.Content.ReadFromJsonAsync<QueryResult>();

        result.Should().NotBeNull();
        result!.QueryId.Should().Be(queryId);
        result.Degraded.Should().BeFalse();
        result.TotalShards.Should().Be(2);
        result.SuccessfulShards.Should().Be(2);
        result.Columns.Should().Equal("id", "amount");
        result.Rows.Should().BeEquivalentTo(new[]
        {
            new[] { "1", "10" },
            new[] { "2", "20" },
            new[] { "3", "30" },
            new[] { "4", "40" }
        });

        var cached = await cache.TryGetResultAsync(queryId);
        cached.Should().NotBeNull();
        cached!.RowCount.Should().Be(4);
    }

    [Fact]
    public async Task PostQueriesStream_ReturnsServerSentEventsFromWorkerShards()
    {
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        await using var shard0 = await SqliteShardDatabase.CreateAsync("e2e-stream-shard-0");
        await using var shard1 = await SqliteShardDatabase.CreateAsync("e2e-stream-shard-1");
        await SeedOrdersAsync(shard0, (1, 10), (2, 20));
        await SeedOrdersAsync(shard1, (3, 30));

        var workerPort = Random.Shared.Next(10000, 60000);
        await using var worker = await WorkerHost.StartAsync(workerPort, shard0.ConnectionString, shard1.ConnectionString);

        var cache = new InMemoryQueryCache();
        using var workerClient = new WorkerGrpcClient(
            NullLogger<WorkerGrpcClient>.Instance,
            new GrpcChannelOptions(),
            null);
        var nodes = new StaticNodeRegistry(
        [
            new NodeInfo("worker-0", "127.0.0.1", workerPort, [0], "test"),
            new NodeInfo("worker-1", "127.0.0.1", workerPort, [1], "test")
        ]);

        await using var factory = new QueryApiFactory(cache, nodes, workerClient);
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/queries/stream")
        {
            Content = JsonContent.Create(new
            {
                queryId = Guid.NewGuid(),
                sql = SelectOrdersSql
            })
        };

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");
        body.Should().Contain("event: metadata");
        body.Should().Contain("event: columns");
        body.Should().Contain("event: row");
        body.Should().Contain("event: complete");
        body.Should().Contain("\"values\":[\"1\",\"10\"]");
    }

    [Fact]
    public async Task PostQueriesPlan_ReturnsShardTargetingMetadata()
    {
        var cache = new InMemoryQueryCache();
        var planner = CreateSqlQueryPlanner();
        var workerClient = new ScriptedWorkerClient(_ => PartialResult.Success(
            Guid.NewGuid(),
            Guid.NewGuid(),
            0,
            [],
            [],
            1));

        await using var factory = new QueryApiFactory(cache, new StaticNodeRegistry([]), workerClient, planner);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/queries/plan", new
        {
            sql = SelectOrdersSql
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var plan = await response.Content.ReadFromJsonAsync<QueryPlanDetails>();
        plan.Should().NotBeNull();
        plan!.SubQueries.Should().NotBeEmpty();
        plan.TargetingStrategy.Should().Be("broadcast");
        plan.MergeInstructions.Should().NotBeNull();
    }

    [Fact]
    public async Task PostQueries_SecondIdenticalQueryUsesCachedPlan()
    {
        var cache = new InMemoryQueryCache();
        var planner = new CountingQueryPlanner(CreateSqlQueryPlanner());
        var workerClient = new ScriptedWorkerClient(static subQuery =>
            PartialResult.Success(
                subQuery.SubQueryId,
                subQuery.ParentQueryId,
                subQuery.ShardIndex,
                [new CoreColumnDescriptor("id", "INTEGER", false), new CoreColumnDescriptor("amount", "INTEGER", false)],
                [[subQuery.ShardIndex.ToString(), (subQuery.ShardIndex * 10).ToString()]],
                executionMs: 1));
        var nodes = new StaticNodeRegistry(
        [
            new NodeInfo("worker-0", "127.0.0.1", 1, [0], "test"),
            new NodeInfo("worker-1", "127.0.0.1", 1, [1], "test")
        ]);

        await using var factory = new QueryApiFactory(cache, nodes, workerClient, planner);
        using var client = factory.CreateClient();

        var firstResponse = await client.PostAsJsonAsync("/queries", new
        {
            queryId = Guid.NewGuid(),
            sql = SelectOrdersSql
        });
        var secondResponse = await client.PostAsJsonAsync("/queries", new
        {
            queryId = Guid.NewGuid(),
            sql = SelectOrdersSql
        });

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK, await firstResponse.Content.ReadAsStringAsync());
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK, await secondResponse.Content.ReadAsStringAsync());
        planner.PlanCallCount.Should().Be(1);
    }

    [Fact]
    public async Task PostQueries_ReturnsPartialContent_WhenOneWorkerIsDown()
    {
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        await using var shard0 = await SqliteShardDatabase.CreateAsync("e2e-partial-shard-0");
        await SeedOrdersAsync(shard0, (1, 10), (2, 20));

        var workerPort = Random.Shared.Next(10000, 60000);
        var unavailablePort = workerPort;
        while (unavailablePort == workerPort)
        {
            unavailablePort = Random.Shared.Next(10000, 60000);
        }

        await using var worker = await WorkerHost.StartAsync(
            workerPort,
            [0],
            new Dictionary<string, string> { ["0"] = shard0.ConnectionString });

        var cache = new InMemoryQueryCache();
        using var workerClient = new WorkerGrpcClient(
            NullLogger<WorkerGrpcClient>.Instance,
            new GrpcChannelOptions(),
            null);
        var nodes = new StaticNodeRegistry(
        [
            new NodeInfo("worker-0", "127.0.0.1", workerPort, [0], "test"),
            new NodeInfo("worker-1", "127.0.0.1", unavailablePort, [1], "test")
        ]);

        await using var factory = new QueryApiFactory(cache, nodes, workerClient);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/queries", new
        {
            queryId = Guid.NewGuid(),
            sql = SelectOrdersSql
        });

        var responseBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.PartialContent, responseBody);
        var result = await response.Content.ReadFromJsonAsync<QueryResult>();

        result.Should().NotBeNull();
        result!.Degraded.Should().BeTrue();
        result.FailedShards.Should().Equal(1);
        result.TotalShards.Should().Be(2);
        result.SuccessfulShards.Should().Be(1);
        result.Rows.Should().BeEquivalentTo(new[]
        {
            new[] { "1", "10" },
            new[] { "2", "20" }
        });
    }

    [Fact]
    public async Task PostQueries_ReturnsPartialResult_WhenQueryTimeoutExpires()
    {
        var cache = new InMemoryQueryCache();
        var workerClient = new ScriptedWorkerClient(async (subQuery, cancellationToken) =>
        {
            if (subQuery.ShardIndex == 1)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }

            return PartialResult.Success(
                subQuery.SubQueryId,
                subQuery.ParentQueryId,
                subQuery.ShardIndex,
                [new CoreColumnDescriptor("id", "INTEGER", false), new CoreColumnDescriptor("amount", "INTEGER", false)],
                [[subQuery.ShardIndex.ToString(), (subQuery.ShardIndex * 10).ToString()]],
                executionMs: 1);
        });
        var nodes = new StaticNodeRegistry(
        [
            new NodeInfo("worker-0", "127.0.0.1", 1, [0], "test"),
            new NodeInfo("worker-1", "127.0.0.1", 1, [1], "test")
        ]);

        await using var factory = new QueryApiFactory(
            cache,
            nodes,
            workerClient,
            coordinatorOptions: CreateCoordinatorOptions(defaultQueryTimeoutMs: 1_000, maxQueryTimeoutMs: 1_000));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/queries", new
        {
            queryId = Guid.NewGuid(),
            sql = SelectOrdersSql,
            timeoutSeconds = 1
        });

        var responseBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.PartialContent, responseBody);
        var result = await response.Content.ReadFromJsonAsync<QueryResult>();

        result.Should().NotBeNull();
        result!.Degraded.Should().BeTrue();
        result.FailedShards.Should().Equal(1);
        result.Rows.Should().ContainSingle(row => row[0] == "0" && row[1] == "0");
    }

    [Fact]
    public async Task PostQueries_AsyncRequestCanPollStatusAndFetchResult()
    {
        var cache = new InMemoryQueryCache();
        var workerClient = new ScriptedWorkerClient(static subQuery =>
            PartialResult.Success(
                subQuery.SubQueryId,
                subQuery.ParentQueryId,
                subQuery.ShardIndex,
                [new CoreColumnDescriptor("id", "INTEGER", false), new CoreColumnDescriptor("amount", "INTEGER", false)],
                [[subQuery.ShardIndex.ToString(), (subQuery.ShardIndex * 10).ToString()]],
                executionMs: 1));
        var nodes = new StaticNodeRegistry(
        [
            new NodeInfo("worker-0", "127.0.0.1", 1, [0], "test"),
            new NodeInfo("worker-1", "127.0.0.1", 1, [1], "test")
        ]);

        await using var factory = new QueryApiFactory(cache, nodes, workerClient);
        using var client = factory.CreateClient();

        var submitResponse = await client.PostAsJsonAsync("/queries", new
        {
            queryId = Guid.NewGuid(),
            sql = SelectOrdersSql,
            @async = true
        });

        submitResponse.StatusCode.Should().Be(HttpStatusCode.Accepted, await submitResponse.Content.ReadAsStringAsync());
        var accepted = await submitResponse.Content.ReadFromJsonAsync<SubmitQueryResponse>();
        accepted.Should().NotBeNull();
        accepted!.StatusUrl.Should().Be($"/queries/{accepted.QueryId}/status");

        var status = await PollUntilCompletedAsync(client, accepted.QueryId);
        status.Status.Should().Be("completed");

        var resultResponse = await client.GetAsync($"/queries/{accepted.QueryId}/result");
        resultResponse.StatusCode.Should().Be(HttpStatusCode.OK, await resultResponse.Content.ReadAsStringAsync());
        var result = await resultResponse.Content.ReadFromJsonAsync<QueryResult>();

        result.Should().NotBeNull();
        result!.QueryId.Should().Be(accepted.QueryId);
        result.RowCount.Should().Be(2);
        result.Degraded.Should().BeFalse();
    }

    private static async Task SeedOrdersAsync(SqliteShardDatabase database, params (int Id, int Amount)[] rows)
    {
        await database.ExecuteNonQueryAsync(
            "CREATE TABLE orders (id INTEGER PRIMARY KEY, amount INTEGER NOT NULL);");

        foreach (var row in rows)
        {
            await database.ExecuteNonQueryAsync(
                $"INSERT INTO orders (id, amount) VALUES ({row.Id}, {row.Amount});");
        }
    }

    private static SqlQueryParser CreateSqlQueryPlanner()
    {
        var options = new ShardMapOptions();
        options.Tables["orders"] = new TableShardConfig
        {
            ShardKey = "id",
            ShardCount = 2,
            Strategy = "RangePartition",
            Ranges =
            [
                new RangePartitionEntry { Shard = 0, Min = "0", Max = "2" },
                new RangePartitionEntry { Shard = 1, Min = "3", Max = "9" }
            ]
        };

        return new SqlQueryParser(Options.Create(options), NullLogger<SqlQueryParser>.Instance);
    }

    private static CoordinatorOptions CreateCoordinatorOptions(
        int defaultQueryTimeoutMs = 5_000,
        int maxQueryTimeoutMs = 5_000) =>
        new()
        {
            DefaultQueryTimeoutMs = defaultQueryTimeoutMs,
            MaxQueryTimeoutMs = maxQueryTimeoutMs,
            PlanCacheTtlSeconds = 60,
            ResultCacheTtlSeconds = 60,
            FanOut = new FanOutOptions
            {
                MaxConcurrentWorkerCalls = 4,
                PerWorkerTimeoutMs = maxQueryTimeoutMs
            },
            Resilience = new ResilienceOptions
            {
                RetryCount = 1,
                RetryBaseDelayMs = 1,
                CircuitBreakerFailureThreshold = 2,
                CircuitBreakerSamplingDurationSeconds = 5,
                CircuitBreakerBreakDurationSeconds = 5
            }
        };

    private static async Task<QueryStatusResponse> PollUntilCompletedAsync(HttpClient client, Guid queryId)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!timeout.IsCancellationRequested)
        {
            var response = await client.GetAsync($"/queries/{queryId}/status", timeout.Token);
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(timeout.Token));
            var status = await response.Content.ReadFromJsonAsync<QueryStatusResponse>(timeout.Token);
            status.Should().NotBeNull();

            if (status!.Status == "completed")
            {
                return status;
            }

            await Task.Delay(50, timeout.Token);
        }

        throw new TimeoutException($"Query {queryId:D} did not complete before the polling timeout.");
    }

    private sealed class QueryApiFactory : WebApplicationFactory<Program>
    {
        private readonly IQueryCache _cache;
        private readonly INodeRegistry _nodeRegistry;
        private readonly IWorkerClient _workerClient;
        private readonly IQueryPlanner? _queryPlanner;
        private readonly CoordinatorOptions _coordinatorOptions;

        public QueryApiFactory(
            IQueryCache cache,
            INodeRegistry nodeRegistry,
            IWorkerClient workerClient,
            IQueryPlanner? queryPlanner = null,
            CoordinatorOptions? coordinatorOptions = null)
        {
            _cache = cache;
            _nodeRegistry = nodeRegistry;
            _workerClient = workerClient;
            _queryPlanner = queryPlanner;
            _coordinatorOptions = coordinatorOptions ?? CreateCoordinatorOptions();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Authentication:Enabled", "false");
            builder.UseSetting("Redis:ConnectionString", "localhost:6379,abortConnect=false,connectTimeout=100");
            builder.UseSetting("CoordinatorClient:BaseUrl", "http://localhost");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IQueryCache>();
                services.RemoveAll<INodeRegistry>();
                services.RemoveAll<IQueryCoordinatorClient>();
                services.RemoveAll<IQueryPlanner>();
                services.RemoveAll<IOptions<CoordinatorOptions>>();

                services.AddSingleton(_cache);
                services.AddSingleton<IQueryCacheAdmin>(sp => (IQueryCacheAdmin)sp.GetRequiredService<IQueryCache>());
                services.AddSingleton(_nodeRegistry);
                services.AddSingleton<IQueryPlanner>(_queryPlanner ?? CreateSqlQueryPlanner());
                services.AddSingleton<ActiveQueryRegistry>();
                services.AddSingleton<QueryPlanningService>();
                services.AddSingleton<WorkerRouter>();
                services.AddSingleton<FanOutService>();
                services.AddSingleton<IResultMerger, ResultAggregator>();
                services.AddSingleton<CoordinatorService>();
                services.AddHttpClient(nameof(WorkerHealthProbe), client => client.Timeout = TimeSpan.FromSeconds(1));
                services.AddSingleton<WorkerHealthProbe>();
                services.AddSingleton<CoordinatorAdminService>();
                services.AddSingleton<IQueryCoordinatorClient, InProcessCoordinatorClient>();
                services.AddSingleton(_workerClient);
                services.AddSingleton<IOptions<CoordinatorOptions>>(Options.Create(_coordinatorOptions));

                services.Configure<ShardMapOptions>(options =>
                {
                    options.Tables.Clear();
                    options.Tables["orders"] = new TableShardConfig
                    {
                        ShardKey = "id",
                        ShardCount = 2,
                        Strategy = "RangePartition",
                        Ranges =
                        [
                            new RangePartitionEntry { Shard = 0, Min = "0", Max = "2" },
                            new RangePartitionEntry { Shard = 1, Min = "3", Max = "9" }
                        ]
                    };
                });
            });
        }
    }

    private sealed class InProcessCoordinatorClient : IQueryCoordinatorClient
    {
        private readonly CoordinatorService _coordinatorService;
        private readonly CoordinatorAdminService _adminService;

        public InProcessCoordinatorClient(
            CoordinatorService coordinatorService,
            CoordinatorAdminService adminService)
        {
            _coordinatorService = coordinatorService;
            _adminService = adminService;
        }

        public Task<QueryResult> ExecuteAsync(QueryRequest request, CancellationToken cancellationToken = default) =>
            _coordinatorService.ExecuteQueryAsync(request, cancellationToken);

        public Task<QueryPlanDetails> PlanAsync(QueryRequest request, CancellationToken cancellationToken = default) =>
            _coordinatorService.PlanQueryAsync(request, cancellationToken);

        public IAsyncEnumerable<QueryStreamEvent> StreamExecuteAsync(
            QueryRequest request,
            CancellationToken cancellationToken = default) =>
            _coordinatorService.StreamExecuteQueryAsync(request, cancellationToken);

        public Task<AdminDashboardStats> GetAdminDashboardAsync(CancellationToken cancellationToken = default) =>
            _adminService.GetDashboardStatsAsync(cancellationToken);

        public Task<ActiveQueryPage> GetActiveQueriesAsync(
            int limit,
            int offset,
            CancellationToken cancellationToken = default) =>
            _adminService.GetActiveQueriesAsync(limit, offset, cancellationToken);

        public Task<CancelQueryResult> CancelQueryAsync(Guid queryId, CancellationToken cancellationToken = default) =>
            _adminService.CancelQueryAsync(queryId, cancellationToken);

        public Task<WorkerHealthDashboard> GetWorkerHealthAsync(CancellationToken cancellationToken = default) =>
            _adminService.GetWorkerHealthAsync(cancellationToken);

        public Task SubmitAsync(QueryRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = Task.Run(async () =>
            {
                try
                {
                    await _coordinatorService.ExecuteQueryAsync(request, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Background query {request.QueryId:D} failed.", ex);
                }
            });

            return Task.CompletedTask;
        }
    }

    private sealed class CountingQueryPlanner : IQueryPlanner
    {
        private readonly IQueryPlanner _inner;
        private int _planCallCount;

        public CountingQueryPlanner(IQueryPlanner inner)
        {
            _inner = inner;
        }

        public int PlanCallCount => Volatile.Read(ref _planCallCount);

        public async Task<QueryPlan> PlanAsync(QueryRequest request, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _planCallCount);
            return await _inner.PlanAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class ScriptedWorkerClient : IWorkerClient
    {
        private readonly Func<SubQuery, CancellationToken, Task<PartialResult>> _handler;

        public ScriptedWorkerClient(Func<SubQuery, PartialResult> handler)
        {
            _handler = (subQuery, _) => Task.FromResult(handler(subQuery));
        }

        public ScriptedWorkerClient(Func<SubQuery, CancellationToken, Task<PartialResult>> handler)
        {
            _handler = handler;
        }

        public async IAsyncEnumerable<PartialResult> ExecuteAsync(
            SubQuery subQuery,
            NodeInfo node,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return await _handler(subQuery, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class InMemoryQueryCache : IQueryCache, IQueryCacheAdmin
    {
        private readonly ConcurrentDictionary<string, QueryPlan> _plans = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<Guid, QueryResult> _results = new();

        public Task<QueryPlan?> TryGetPlanAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _plans.TryGetValue(cacheKey, out var plan);
            return Task.FromResult(plan);
        }

        public Task SetPlanAsync(
            string cacheKey,
            QueryPlan plan,
            TimeSpan ttl,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _plans[cacheKey] = plan;
            return Task.CompletedTask;
        }

        public Task<QueryResult?> TryGetResultAsync(Guid queryId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _results.TryGetValue(queryId, out var result);
            return Task.FromResult(result);
        }

        public Task SetResultAsync(
            Guid queryId,
            QueryResult result,
            TimeSpan ttl,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _results[queryId] = result;
            return Task.CompletedTask;
        }

        public Task<AdminCacheStats> GetStatsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new AdminCacheStats(_plans.Count, _results.Count, 0, DateTimeOffset.UtcNow));
        }

        public Task<AdminCacheFlushResult> FlushPlansAsync(string? planHash = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            long deleted;
            if (string.IsNullOrWhiteSpace(planHash))
            {
                deleted = _plans.Count;
                _plans.Clear();
            }
            else
            {
                var key = $"plan::{planHash}";
                deleted = _plans.Remove(key, out _) ? 1 : 0;
            }

            return Task.FromResult(new AdminCacheFlushResult(
                deleted,
                string.IsNullOrWhiteSpace(planHash) ? "all_plans" : $"plan_hash:{planHash}",
                DateTimeOffset.UtcNow));
        }
    }

    private sealed class StaticNodeRegistry : INodeRegistry
    {
        private readonly IReadOnlyList<NodeInfo> _nodes;

        public StaticNodeRegistry(IReadOnlyList<NodeInfo> nodes)
        {
            _nodes = nodes;
        }

        public Task<IReadOnlyList<NodeInfo>> GetHealthyNodesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_nodes);
        }

        public Task RegisterAsync(NodeInfo node, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeregisterAsync(string nodeId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class WorkerHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private WorkerHost(WebApplication app)
        {
            _app = app;
        }

        public static async Task<WorkerHost> StartAsync(
            int port,
            string shard0ConnectionString,
            string shard1ConnectionString) =>
            await StartAsync(
                port,
                [0, 1],
                new Dictionary<string, string>
                {
                    ["0"] = shard0ConnectionString,
                    ["1"] = shard1ConnectionString
                });

        public static async Task<WorkerHost> StartAsync(
            int port,
            IReadOnlyList<int> shardIndices,
            Dictionary<string, string> shards)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenLocalhost(port, listenOptions => listenOptions.Protocols = HttpProtocols.Http2);
            });

            builder.Services.AddSingleton(Options.Create(new WorkerOptions
            {
                NodeId = "e2e-worker",
                ShardIndices = shardIndices,
                Shards = shards,
                Execution = new WorkerExecutionOptions
                {
                    StreamChunkSize = 1,
                    CommandTimeoutSeconds = 5,
                    MaxConcurrentQueries = 4
                }
            }));
            builder.Services.AddSingleton<ShardConnectionResolver>();
            builder.Services.AddSingleton<ShardExecutor>();
            builder.Services.AddSingleton<ISubQueryExecutor>(serviceProvider =>
                serviceProvider.GetRequiredService<ShardExecutor>());
            builder.Services.AddGrpc(options => options.Interceptors.Add<TracingServerInterceptor>());

            var app = builder.Build();
            app.MapGrpcService<WorkerGrpcService>();
            await app.StartAsync();
            return new WorkerHost(app);
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    private sealed class SqliteShardDatabase : IAsyncDisposable
    {
        private static int _providerInitialized;
        private readonly SqliteConnection _keeperConnection;

        private SqliteShardDatabase(SqliteConnection keeperConnection, string connectionString)
        {
            _keeperConnection = keeperConnection;
            ConnectionString = connectionString;
        }

        public string ConnectionString { get; }

        public static async Task<SqliteShardDatabase> CreateAsync(string databaseName)
        {
            InitializeProvider();

            var connectionString = $"Data Source={databaseName};Mode=Memory;Cache=Shared";
            var keeperConnection = new SqliteConnection(connectionString);
            await keeperConnection.OpenAsync();
            return new SqliteShardDatabase(keeperConnection, connectionString);
        }

        public async Task ExecuteNonQueryAsync(string sql)
        {
            await using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }

        public async ValueTask DisposeAsync() => await _keeperConnection.DisposeAsync();

        private static void InitializeProvider()
        {
            if (Interlocked.Exchange(ref _providerInitialized, 1) == 1)
            {
                return;
            }

            raw.SetProvider(new SQLite3Provider_sqlite3());
        }
    }
}
