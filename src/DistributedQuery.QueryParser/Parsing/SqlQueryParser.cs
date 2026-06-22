using DistributedQuery.Core.Exceptions;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace DistributedQuery.QueryParser.Parsing;

/// <summary>
/// Implements IQueryPlanner. Orchestrates: validate -> parse -> visit AST -> resolve shards -> split.
/// Stateless and thread-safe; safe to register as Singleton.
/// </summary>
public sealed class SqlQueryParser : IQueryPlanner
{
    private readonly ShardMapOptions _shardMap;
    private readonly ShardTargetResolver _resolver;
    private readonly QuerySplitter _splitter;
    private readonly ILogger<SqlQueryParser> _logger;

    public SqlQueryParser(IOptions<ShardMapOptions> shardMap, ILogger<SqlQueryParser> logger)
    {
        _shardMap = shardMap.Value;
        _resolver = new ShardTargetResolver(_shardMap);
        _splitter = new QuerySplitter();
        _logger = logger;
    }

    public Task<QueryPlan> PlanAsync(QueryRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var planHash = PlanHashComputer.ComputeHash(request.Sql, request.Parameters);

        _logger.LogDebug("Planning query {QueryId} with hash {PlanHash}", request.QueryId, planHash);

        var script = SqlScriptParser.ParseAndValidate(request.Sql);
        var metadata = ExtractMetadata(script);

        if (metadata.PrimaryTable is null)
        {
            throw new QueryParseException(
                "Could not determine primary table from SQL",
                planHash,
                ["No FROM clause or table reference found"]);
        }

        if (!_shardMap.Tables.TryGetValue(metadata.PrimaryTable.ToLowerInvariant(), out var tableConfig))
        {
            throw new QueryParseException(
                $"Table '{metadata.PrimaryTable}' is not configured in the shard map.",
                planHash,
                [$"No shard map configuration found for table '{metadata.PrimaryTable}'."]);
        }

        var targetShards = _resolver.Resolve(metadata.PrimaryTable, metadata.ShardKeyPredicate);

        _logger.LogDebug(
            "Query {QueryId} targets {ShardCount} shard(s) of {TotalShards} for table '{Table}'",
            request.QueryId, targetShards.Count, tableConfig.ShardCount, metadata.PrimaryTable);

        var (subQueries, mergeInstructions) = _splitter.Split(
            request,
            script,
            metadata,
            targetShards,
            tableConfig.ShardCount);

        var plan = QueryPlan.Create(planHash, subQueries, mergeInstructions);

        return Task.FromResult(plan);
    }

    private QueryMetadata ExtractMetadata(TSqlScript script)
    {
        var tablePreVisitor = new TableNameVisitor();
        script.Accept(tablePreVisitor);
        var tableName = tablePreVisitor.PrimaryTable;

        var shardKey = tableName is not null
            && _shardMap.Tables.TryGetValue(tableName.ToLowerInvariant(), out var cfg)
            ? cfg.ShardKey
            : string.Empty;

        var visitor = new AstVisitor(shardKey);
        script.Accept(visitor);

        return visitor.Metadata;
    }

    private sealed class TableNameVisitor : TSqlFragmentVisitor
    {
        public string? PrimaryTable { get; private set; }

        public override void Visit(NamedTableReference node)
        {
            PrimaryTable ??= node.SchemaObject.BaseIdentifier.Value;
        }
    }
}
