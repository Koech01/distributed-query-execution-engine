using System.Diagnostics.Metrics;

namespace DistributedQuery.Infrastructure.Observability;

public static class DqeeMetrics
{
    public static readonly Meter Api = new("dqee.api", "1.0.0");
    public static readonly Meter Coordinator = new("dqee.coordinator", "1.0.0");
    public static readonly Meter Worker = new("dqee.worker", "1.0.0");
    public static readonly Meter Grpc = new("dqee.grpc", "1.0.0");

    public static readonly Counter<long> ApiRequestsTotal =
        Api.CreateCounter<long>("dqee_api_requests_total", description: "Total HTTP requests");

    public static readonly Histogram<double> ApiRequestDurationSeconds =
        Api.CreateHistogram<double>("dqee_api_request_duration_seconds", unit: "s", description: "HTTP request latency");

    public static readonly UpDownCounter<long> ApiActiveRequests =
        Api.CreateUpDownCounter<long>("dqee_api_active_requests", description: "Currently in-flight HTTP requests");

    public static readonly Counter<long> CoordinatorQueriesTotal =
        Coordinator.CreateCounter<long>("dqee_coordinator_queries_total", description: "Queries processed");

    public static readonly Histogram<double> CoordinatorQueryDurationSeconds =
        Coordinator.CreateHistogram<double>("dqee_coordinator_query_duration_seconds", unit: "s", description: "End-to-end query latency");

    public static readonly Counter<long> CoordinatorPlanCacheHitsTotal =
        Coordinator.CreateCounter<long>("dqee_coordinator_plan_cache_hits_total", description: "Plan cache hits");

    public static readonly Counter<long> CoordinatorPlanCacheMissesTotal =
        Coordinator.CreateCounter<long>("dqee_coordinator_plan_cache_misses_total", description: "Plan cache misses");

    public static readonly Histogram<int> CoordinatorFanOutSize =
        Coordinator.CreateHistogram<int>("dqee_coordinator_fanout_size", description: "Number of workers per query");

    public static readonly Histogram<double> CoordinatorMergeDurationSeconds =
        Coordinator.CreateHistogram<double>("dqee_coordinator_merge_duration_seconds", unit: "s", description: "Result merge latency");

    public static readonly Counter<long> CoordinatorDegradedQueriesTotal =
        Coordinator.CreateCounter<long>("dqee_coordinator_degraded_queries_total", description: "Queries with partial failures");

    public static readonly Counter<long> WorkerSubQueriesTotal =
        Worker.CreateCounter<long>("dqee_worker_subqueries_total", description: "Sub-queries executed");

    public static readonly Histogram<double> WorkerSubqueryDurationSeconds =
        Worker.CreateHistogram<double>("dqee_worker_subquery_duration_seconds", unit: "s", description: "Sub-query execution latency");

    public static readonly Counter<long> WorkerRowsReturnedTotal =
        Worker.CreateCounter<long>("dqee_worker_rows_returned_total", description: "Total rows streamed");

    public static readonly UpDownCounter<long> WorkerActiveQueries =
        Worker.CreateUpDownCounter<long>("dqee_worker_active_queries", description: "Currently executing sub-queries");

    public static readonly UpDownCounter<long> WorkerDbConnectionPoolSize =
        Worker.CreateUpDownCounter<long>("dqee_worker_db_connection_pool_size", description: "DB connection pool usage");

    public static readonly Histogram<double> GrpcClientDurationSeconds =
        Grpc.CreateHistogram<double>("dqee_grpc_client_duration_seconds", unit: "s", description: "Client-side gRPC call latency");

    public static readonly Histogram<double> GrpcServerDurationSeconds =
        Grpc.CreateHistogram<double>("dqee_grpc_server_duration_seconds", unit: "s", description: "Server-side gRPC call latency");
}
