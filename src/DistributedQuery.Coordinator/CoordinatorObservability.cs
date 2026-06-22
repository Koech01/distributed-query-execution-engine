using DistributedQuery.Core.Models;
using DistributedQuery.Infrastructure.Observability;

namespace DistributedQuery.Coordinator;

internal static class CoordinatorObservability
{
    public static void RecordPlanCache(bool fromCache)
    {
        if (fromCache)
        {
            DqeeMetrics.CoordinatorPlanCacheHitsTotal.Add(1);
        }
        else
        {
            DqeeMetrics.CoordinatorPlanCacheMissesTotal.Add(1);
        }
    }

    public static void RecordFanOutSize(int size) =>
        DqeeMetrics.CoordinatorFanOutSize.Record(size);

    public static void RecordMergeDuration(TimeSpan duration) =>
        DqeeMetrics.CoordinatorMergeDurationSeconds.Record(duration.TotalSeconds);

    public static void RecordQueryCompletion(QueryResult result, TimeSpan duration)
    {
        var status = ResolveStatus(result);
        DqeeMetrics.CoordinatorQueriesTotal.Add(1, new KeyValuePair<string, object?>("status", status));
        DqeeMetrics.CoordinatorQueryDurationSeconds.Record(duration.TotalSeconds, new KeyValuePair<string, object?>("status", status));

        if (result.Degraded)
        {
            DqeeMetrics.CoordinatorDegradedQueriesTotal.Add(1);
        }
    }

    private static string ResolveStatus(QueryResult result)
    {
        if (result.FailedShards.Count == 0)
        {
            return "success";
        }

        return result.Degraded ? "partial" : "failed";
    }
}
