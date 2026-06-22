using DistributedQuery.Core.Models;

namespace DistributedQuery.Core.Interfaces;

public interface IQueryPlanner
{
    Task<QueryPlan> PlanAsync(QueryRequest request, CancellationToken cancellationToken = default);
}
