using DistributedQuery.Core.Models;
using DistributedQuery.QueryParser.Parsing;

namespace DistributedQuery.Infrastructure.Caching;

/// <summary>
/// Builds deterministic, fixed-length Redis cache keys from SQL and parameter metadata.
/// Parameter values are intentionally excluded - only types are used, so queries that
/// differ only in parameter values share the same plan cache entry.
/// </summary>
public static class CacheKeyBuilder
{
    private const string PlanPrefix = "plan::";
    private const string ResultPrefix = "result::";

    /// <summary>
    /// Builds a plan cache key: plan::{sha256(normalized_sql::param_type_sig)}
    /// </summary>
    public static string ForPlan(string sql, IReadOnlyList<QueryParameter> parameters) =>
        PlanPrefix + PlanHashComputer.ComputeHash(sql, parameters);

    /// <summary>
    /// Builds a result cache key: result::{queryId}
    /// </summary>
    public static string ForResult(Guid queryId) =>
        ResultPrefix + queryId.ToString("N");

    /// <summary>
    /// Exposed for unit tests. Delegates to the shared PlanHashComputer implementation.
    /// </summary>
    public static string NormalizeSql(string sql) => PlanHashComputer.NormalizeSql(sql);
}
