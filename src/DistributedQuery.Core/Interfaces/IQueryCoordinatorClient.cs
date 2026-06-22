using DistributedQuery.Core.Models;

namespace DistributedQuery.Core.Interfaces;

/// <summary>
/// Forwards query execution requests from the API host to the coordinator process.
/// </summary>
public interface IQueryCoordinatorClient
{
    Task<QueryResult> ExecuteAsync(QueryRequest request, CancellationToken cancellationToken = default);

    Task SubmitAsync(QueryRequest request, CancellationToken cancellationToken = default);

    Task<QueryPlanDetails> PlanAsync(QueryRequest request, CancellationToken cancellationToken = default);

    IAsyncEnumerable<QueryStreamEvent> StreamExecuteAsync(
        QueryRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminDashboardStats> GetAdminDashboardAsync(CancellationToken cancellationToken = default);

    Task<ActiveQueryPage> GetActiveQueriesAsync(
        int limit,
        int offset,
        CancellationToken cancellationToken = default);

    Task<CancelQueryResult> CancelQueryAsync(Guid queryId, CancellationToken cancellationToken = default);

    Task<WorkerHealthDashboard> GetWorkerHealthAsync(CancellationToken cancellationToken = default);
}
