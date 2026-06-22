using DistributedQuery.Core.Exceptions;
using DistributedQuery.Core.Models;
using DistributedQuery.QueryParser.Parsing;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DistributedQuery.UnitTests.QueryParser;

public sealed class SqlQueryParserTests
{
    private static SqlQueryParser BuildParser(int shardCount = 4, string strategy = "ConsistentHash") =>
        new(Options.Create(new ShardMapOptions
        {
            Tables =
            {
                ["orders"] = new TableShardConfig
                {
                    ShardKey = "customer_id",
                    ShardCount = shardCount,
                    Strategy = strategy
                }
            }
        }), NullLogger<SqlQueryParser>.Instance);

    private static QueryRequest Req(string sql) => QueryRequest.Create(sql);

    // --- Shard routing ---

    [Fact]
    public async Task PlanAsync_EqualityOnShardKey_ProducesSingleSubQuery()
    {
        var parser = BuildParser();
        var plan = await parser.PlanAsync(Req("SELECT id FROM orders WHERE customer_id = 42"));
        plan.SubQueries.Should().HaveCount(1);
    }

    [Fact]
    public async Task PlanAsync_NoShardKeyPredicate_BroadcastsToAllShards()
    {
        var parser = BuildParser(shardCount: 4);
        var plan = await parser.PlanAsync(Req("SELECT id FROM orders WHERE status = 'active'"));
        plan.SubQueries.Should().HaveCount(4);
    }

    [Fact]
    public async Task PlanAsync_InListOnShardKey_TargetsDistinctShards()
    {
        var parser = BuildParser(shardCount: 8);
        var plan = await parser.PlanAsync(Req("SELECT id FROM orders WHERE customer_id IN (1, 2, 3)"));
        plan.SubQueries.Count.Should().BeLessThanOrEqualTo(3);
        plan.SubQueries.Select(s => s.ShardIndex).Should().OnlyHaveUniqueItems();
    }

    // --- ORDER BY removed from sub-queries, preserved in MergeInstructions ---

    [Fact]
    public async Task PlanAsync_OrderBy_RemovedFromSubQuerySql()
    {
        var parser = BuildParser();
        var plan = await parser.PlanAsync(Req("SELECT id, amount FROM orders ORDER BY amount DESC"));
        plan.SubQueries.Should().AllSatisfy(sq =>
            sq.Sql.Should().NotContainEquivalentOf("ORDER BY"));
    }

    [Fact]
    public async Task PlanAsync_OrderBy_PreservedInMergeInstructions()
    {
        var parser = BuildParser();
        var plan = await parser.PlanAsync(Req("SELECT id, amount FROM orders ORDER BY amount DESC"));
        plan.MergeInstructions.OrderBy.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new OrderByColumn("amount", Descending: true));
    }

    // --- TOP over-fetch ---

    [Fact]
    public async Task PlanAsync_TopN_AppliesOverFetchInSubQueries()
    {
        var parser = BuildParser();
        var plan = await parser.PlanAsync(Req("SELECT TOP 10 id FROM orders ORDER BY id"));
        // Each sub-query should have TOP 20 (10 * 2 over-fetch multiplier)
        plan.SubQueries.Should().AllSatisfy(sq =>
            sq.Sql.Should().ContainEquivalentOf("TOP 20"));
    }

    [Fact]
    public async Task PlanAsync_TopN_PreservedInMergeInstructions()
    {
        var parser = BuildParser();
        var plan = await parser.PlanAsync(Req("SELECT TOP 10 id FROM orders"));
        plan.MergeInstructions.Limit.Should().Be(10);
    }

    // --- Aggregates ---

    [Fact]
    public async Task PlanAsync_SumAggregate_PreservedInMergeInstructions()
    {
        var parser = BuildParser();
        var plan = await parser.PlanAsync(Req("SELECT SUM(amount) AS total FROM orders"));
        plan.MergeInstructions.Aggregates.Should().ContainSingle()
            .Which.Function.Should().Be(AggregateFunction.Sum);
    }

    [Fact]
    public async Task PlanAsync_AvgAggregate_MappedToAvgInMergeInstructions()
    {
        var parser = BuildParser();
        var plan = await parser.PlanAsync(Req("SELECT AVG(amount) AS avg_amount FROM orders"));
        plan.MergeInstructions.Aggregates.Should().ContainSingle()
            .Which.Function.Should().Be(AggregateFunction.Avg);
    }

    // --- DISTINCT ---

    [Fact]
    public async Task PlanAsync_Distinct_PropagatedToMergeInstructions()
    {
        var parser = BuildParser();
        var plan = await parser.PlanAsync(Req("SELECT DISTINCT status FROM orders"));
        plan.MergeInstructions.IsDistinct.Should().BeTrue();
    }

    [Fact]
    public async Task PlanAsync_GroupByAggregate_PreservedInMergeInstructions()
    {
        var parser = BuildParser();
        var plan = await parser.PlanAsync(Req("SELECT status, COUNT(*) AS cnt FROM orders GROUP BY status"));
        plan.MergeInstructions.Aggregates.Should().ContainSingle()
            .Which.Function.Should().Be(AggregateFunction.Count);
    }

    [Fact]
    public async Task PlanAsync_SingleShardRoute_UsesClusterTotalShards()
    {
        var parser = BuildParser(shardCount: 8);
        var plan = await parser.PlanAsync(Req("SELECT id FROM orders WHERE customer_id = 42"));
        plan.SubQueries.Should().ContainSingle()
            .Which.TotalShards.Should().Be(8);
    }

    [Fact]
    public async Task PlanAsync_PlanHashMatchesCacheKeyBuilderHashSuffix()
    {
        var parser = BuildParser();
        var sql = "SELECT id FROM orders WHERE customer_id = @id";
        var parameters = new List<QueryParameter> { new("@id", "int", "1") };
        var request = QueryRequest.Create(sql, parameters);

        var plan = await parser.PlanAsync(request);
        var cacheKey = DistributedQuery.Infrastructure.Caching.CacheKeyBuilder.ForPlan(sql, parameters);

        plan.PlanHash.Should().Be(cacheKey["plan::".Length..]);
    }

    // --- Plan hash stability ---

    [Fact]
    public async Task PlanAsync_SameSqlDifferentWhitespace_ProducesSamePlanHash()
    {
        var parser = BuildParser();
        var plan1 = await parser.PlanAsync(Req("SELECT id FROM orders WHERE customer_id = 1"));
        var plan2 = await parser.PlanAsync(Req("  SELECT  id  FROM  orders  WHERE  customer_id = 1  "));
        plan1.PlanHash.Should().Be(plan2.PlanHash);
    }

    [Fact]
    public async Task PlanAsync_DifferentSql_ProducesDifferentPlanHash()
    {
        var parser = BuildParser();
        var plan1 = await parser.PlanAsync(Req("SELECT id FROM orders WHERE customer_id = 1"));
        var plan2 = await parser.PlanAsync(Req("SELECT id FROM orders WHERE status = 'active'"));
        plan1.PlanHash.Should().NotBe(plan2.PlanHash);
    }

    // --- Rejection ---

    [Fact]
    public async Task PlanAsync_InsertStatement_ThrowsQueryParseException()
    {
        var parser = BuildParser();
        var act = async () => await parser.PlanAsync(Req("INSERT INTO orders VALUES (1)"));
        await act.Should().ThrowAsync<QueryParseException>();
    }

    [Fact]
    public async Task PlanAsync_MalformedSql_ThrowsQueryParseException()
    {
        var parser = BuildParser();
        var act = async () => await parser.PlanAsync(Req("SELECT FROM WHERE"));
        await act.Should().ThrowAsync<QueryParseException>();
    }

    [Fact]
    public async Task PlanAsync_UnknownTable_ThrowsQueryParseExceptionWithTableName()
    {
        var parser = BuildParser();
        var act = async () => await parser.PlanAsync(Req("SELECT * FROM nonexistent_table_xyz"));

        var exception = await act.Should().ThrowAsync<QueryParseException>();
        exception.Which.Message.Should().Contain("nonexistent_table_xyz");
        exception.Which.ParseErrors.Should().ContainSingle()
            .Which.Should().Contain("nonexistent_table_xyz");
    }

    // --- SubQuery metadata ---

    [Fact]
    public async Task PlanAsync_SubQueries_HaveCorrectTotalShards()
    {
        var parser = BuildParser(shardCount: 4);
        var plan = await parser.PlanAsync(Req("SELECT id FROM orders WHERE status = 'x'"));
        plan.SubQueries.Should().AllSatisfy(sq => sq.TotalShards.Should().Be(4));
    }

    [Fact]
    public async Task PlanAsync_SubQueries_HaveUniqueSubQueryIds()
    {
        var parser = BuildParser(shardCount: 4);
        var plan = await parser.PlanAsync(Req("SELECT id FROM orders"));
        plan.SubQueries.Select(sq => sq.SubQueryId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task PlanAsync_SubQueries_ShareParentQueryId()
    {
        var parser = BuildParser(shardCount: 4);
        var request = Req("SELECT id FROM orders");
        var plan = await parser.PlanAsync(request);
        plan.SubQueries.Should().AllSatisfy(sq =>
            sq.ParentQueryId.Should().Be(request.QueryId));
    }
}
