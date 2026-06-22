using DistributedQuery.Core.Models;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace DistributedQuery.QueryParser.Parsing;

/// <summary>
/// Produces a SubQuery per shard target from a parsed QueryMetadata.
/// Rewrites SQL once per plan: removes ORDER BY and TOP over-fetch (applied at merge).
/// AVG merge is handled by ResultAggregator via SUM+COUNT from workers.
/// </summary>
public sealed class QuerySplitter
{
    private const int OverFetchMultiplier = 2;

    public (IReadOnlyList<SubQuery> SubQueries, MergeInstructions MergeInstructions) Split(
        QueryRequest request,
        TSqlScript script,
        QueryMetadata metadata,
        IReadOnlyList<int> targetShards,
        int clusterShardCount)
    {
        var mergeInstructions = BuildMergeInstructions(metadata);
        var rewrittenSql = RewriteSql(script);
        var subQueries = new List<SubQuery>(targetShards.Count);

        foreach (var shardIndex in targetShards)
        {
            subQueries.Add(SubQuery.Create(
                request.QueryId,
                rewrittenSql,
                nodeId: string.Empty,
                shardIndex,
                clusterShardCount,
                request.Parameters,
                timeoutMs: request.Timeout is null ? 0 : (int)Math.Min(int.MaxValue, request.Timeout.Value.TotalMilliseconds)));
        }

        return (subQueries, mergeInstructions);
    }

    private static string RewriteSql(TSqlScript script)
    {
        var rewriter = new SqlRewriter();
        script.Accept(rewriter);

        var generator = new Sql160ScriptGenerator(new SqlScriptGeneratorOptions
        {
            NewLineBeforeFromClause = false,
            NewLineBeforeWhereClause = false,
            NewLineBeforeOrderByClause = false
        });
        generator.GenerateScript(script, out var rewritten);
        return rewritten.Trim();
    }

    private static MergeInstructions BuildMergeInstructions(QueryMetadata metadata)
    {
        var orderBy = metadata.OrderByColumns
            .Select((col, i) => new OrderByColumn(col, metadata.OrderByDescending[i]))
            .ToList();

        var aggregates = metadata.Aggregates
            .Select(a => new AggregateOperation(
                MapFunction(a.FunctionName),
                a.ColumnExpression,
                a.Alias))
            .ToList();

        return new MergeInstructions(
            OrderBy: orderBy,
            Aggregates: aggregates,
            Limit: metadata.TopCount,
            Offset: null,
            IsDistinct: metadata.IsDistinct);
    }

    private static AggregateFunction MapFunction(string name) => name.ToUpperInvariant() switch
    {
        "SUM"   => AggregateFunction.Sum,
        "COUNT" => AggregateFunction.Count,
        "AVG"   => AggregateFunction.Avg,
        "MIN"   => AggregateFunction.Min,
        "MAX"   => AggregateFunction.Max,
        _       => throw new ArgumentOutOfRangeException(nameof(name), name, "Unsupported aggregate function.")
    };

    /// <summary>
    /// Mutates the AST in-place to produce a sub-query-safe SQL:
    /// - Removes ORDER BY (applied at merge)
    /// - Applies over-fetch to TOP (TOP N becomes TOP N*2 per shard)
    /// </summary>
    private sealed class SqlRewriter : TSqlFragmentVisitor
    {
        public override void Visit(QueryExpression node) { }

        public override void ExplicitVisit(QuerySpecification node)
        {
            if (node.TopRowFilter?.Expression is IntegerLiteral topLit
                && int.TryParse(topLit.Value, out var topN))
            {
                topLit.Value = (topN * OverFetchMultiplier).ToString();
            }

            node.OrderByClause = null;

            base.ExplicitVisit(node);
        }
    }
}
