using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace DistributedQuery.QueryParser.Parsing;

/// <summary>
/// Walks a TSql AST and extracts metadata needed for shard targeting and merge planning.
/// Stateless and reusable per parse call.
/// </summary>
public sealed class QueryMetadata
{
    public string? PrimaryTable { get; set; }
    public ShardKeyPredicate? ShardKeyPredicate { get; set; }
    public List<string> OrderByColumns { get; } = [];
    public List<bool> OrderByDescending { get; } = [];
    public List<AggregateRef> Aggregates { get; } = [];
    public List<string> GroupByColumns { get; } = [];
    public bool IsDistinct { get; set; }
    public int? TopCount { get; set; }
}

public sealed record ShardKeyPredicate(
    ShardPredicateType Type,
    string? EqualityValue,
    string? RangeMin,
    string? RangeMax,
    IReadOnlyList<string>? InValues
);

public enum ShardPredicateType { Equality, Range, InList }

public sealed record AggregateRef(string FunctionName, string ColumnExpression, string Alias);

public sealed class AstVisitor : TSqlFragmentVisitor
{
    private readonly string _shardKeyColumn;

    public QueryMetadata Metadata { get; } = new();

    public AstVisitor(string shardKeyColumn)
    {
        _shardKeyColumn = shardKeyColumn.ToLowerInvariant();
    }

    public override void Visit(NamedTableReference node)
    {
        Metadata.PrimaryTable ??= node.SchemaObject.BaseIdentifier.Value;
    }

    public override void Visit(QuerySpecification node)
    {
        Metadata.IsDistinct = node.UniqueRowFilter == UniqueRowFilter.Distinct;

        if (node.TopRowFilter?.Expression is IntegerLiteral topLit
            && int.TryParse(topLit.Value, out var top))
        {
            Metadata.TopCount = top;
        }
    }

    public override void Visit(SelectScalarExpression node)
    {
        if (node.Expression is not FunctionCall fn) return;

        var funcName = fn.FunctionName.Value.ToUpperInvariant();
        if (funcName is not ("SUM" or "COUNT" or "AVG" or "MIN" or "MAX")) return;

        var colExpr = fn.Parameters.Count > 0
            ? ScriptToString(fn.Parameters[0])
            : "*";
        var alias = node.ColumnName?.Value ?? $"__{funcName}_{colExpr}";
        Metadata.Aggregates.Add(new AggregateRef(funcName, colExpr, alias));
    }

    public override void Visit(BooleanComparisonExpression node)
    {
        var left = ScriptToString(node.FirstExpression).ToLowerInvariant().Trim('[', ']');
        if (left != _shardKeyColumn) return;

        var right = ScriptToString(node.SecondExpression);

        switch (node.ComparisonType)
        {
            case BooleanComparisonType.Equals:
                Metadata.ShardKeyPredicate = new ShardKeyPredicate(
                    ShardPredicateType.Equality, right, null, null, null);
                break;

            case BooleanComparisonType.GreaterThan:
            case BooleanComparisonType.GreaterThanOrEqualTo:
            {
                var existing = Metadata.ShardKeyPredicate;
                Metadata.ShardKeyPredicate = new ShardKeyPredicate(
                    ShardPredicateType.Range, null, right, existing?.RangeMax, null);
                break;
            }

            case BooleanComparisonType.LessThan:
            case BooleanComparisonType.LessThanOrEqualTo:
            {
                var existing = Metadata.ShardKeyPredicate;
                Metadata.ShardKeyPredicate = new ShardKeyPredicate(
                    ShardPredicateType.Range, null, existing?.RangeMin, right, null);
                break;
            }
        }
    }

    public override void Visit(InPredicate node)
    {
        var col = ScriptToString(node.Expression).ToLowerInvariant().Trim('[', ']');
        if (col != _shardKeyColumn) return;

        var values = node.Values.Select(ScriptToString).ToList();
        Metadata.ShardKeyPredicate = new ShardKeyPredicate(
            ShardPredicateType.InList, null, null, null, values);
    }

    // BETWEEN x AND y is represented as BooleanTernaryExpression in ScriptDom
    public override void Visit(BooleanTernaryExpression node)
    {
        if (node.TernaryExpressionType != BooleanTernaryExpressionType.Between) return;

        var col = ScriptToString(node.FirstExpression).ToLowerInvariant().Trim('[', ']');
        if (col != _shardKeyColumn) return;

        Metadata.ShardKeyPredicate = new ShardKeyPredicate(
            ShardPredicateType.Range,
            null,
            ScriptToString(node.SecondExpression),
            ScriptToString(node.ThirdExpression),
            null);
    }

    public override void Visit(ExpressionWithSortOrder node)
    {
        Metadata.OrderByColumns.Add(ScriptToString(node.Expression));
        Metadata.OrderByDescending.Add(node.SortOrder == SortOrder.Descending);
    }

    public override void Visit(GroupByClause node)
    {
        foreach (var spec in node.GroupingSpecifications.OfType<ExpressionGroupingSpecification>())
            Metadata.GroupByColumns.Add(ScriptToString(spec.Expression));
    }

    private static string ScriptToString(TSqlFragment fragment)
    {
        var generator = new Sql160ScriptGenerator(new SqlScriptGeneratorOptions
        {
            NewLineBeforeFromClause = false,
            NewLineBeforeWhereClause = false
        });
        generator.GenerateScript(fragment, out var script);
        return script.Trim();
    }
}
