using DistributedQuery.Core.Exceptions;
using DistributedQuery.QueryParser.Parsing;
using FluentAssertions;

namespace DistributedQuery.UnitTests.QueryParser;

public sealed class ShardTargetResolverTests
{
    private static ShardMapOptions EightShardHashMap() => new()
    {
        Tables =
        {
            ["orders"] = new TableShardConfig
            {
                ShardKey = "customer_id",
                ShardCount = 8,
                Strategy = "ConsistentHash"
            }
        }
    };

    private static ShardMapOptions FourShardRangeMap() => new()
    {
        Tables =
        {
            ["products"] = new TableShardConfig
            {
                ShardKey = "product_id",
                ShardCount = 4,
                Strategy = "RangePartition",
                Ranges =
                [
                    new RangePartitionEntry { Shard = 0, Min = "1",     Max = "25000"  },
                    new RangePartitionEntry { Shard = 1, Min = "25001", Max = "50000"  },
                    new RangePartitionEntry { Shard = 2, Min = "50001", Max = "75000"  },
                    new RangePartitionEntry { Shard = 3, Min = "75001", Max = null     }
                ]
            }
        }
    };

    [Theory]
    [InlineData("Orders")]
    [InlineData("ORDERS")]
    public void Resolve_TableLookup_IsCaseInsensitive(string tableName)
    {
        var resolver = new ShardTargetResolver(EightShardHashMap());
        var predicate = new ShardKeyPredicate(ShardPredicateType.Equality, "42", null, null, null);

        var shards = resolver.Resolve(tableName, predicate);

        shards.Should().HaveCount(1);
    }

    [Fact]
    public void Resolve_NoPredicate_BroadcastsToAllShards()
    {
        var resolver = new ShardTargetResolver(EightShardHashMap());
        var shards = resolver.Resolve("orders", predicate: null);
        shards.Should().HaveCount(8).And.BeEquivalentTo([0, 1, 2, 3, 4, 5, 6, 7]);
    }

    [Fact]
    public void Resolve_EqualityPredicate_TargetsSingleShard()
    {
        var resolver = new ShardTargetResolver(EightShardHashMap());
        var predicate = new ShardKeyPredicate(ShardPredicateType.Equality, "42", null, null, null);
        var shards = resolver.Resolve("orders", predicate);
        shards.Should().HaveCount(1);
    }

    [Fact]
    public void Resolve_EqualityPredicate_IsDeterministic()
    {
        var resolver = new ShardTargetResolver(EightShardHashMap());
        var predicate = new ShardKeyPredicate(ShardPredicateType.Equality, "42", null, null, null);
        var first = resolver.Resolve("orders", predicate);
        var second = resolver.Resolve("orders", predicate);
        first.Should().BeEquivalentTo(second);
    }

    [Fact]
    public void Resolve_InListPredicate_TargetsDistinctShards()
    {
        var resolver = new ShardTargetResolver(EightShardHashMap());
        var predicate = new ShardKeyPredicate(ShardPredicateType.InList, null, null, null, ["1", "2", "3"]);
        var shards = resolver.Resolve("orders", predicate);
        shards.Should().OnlyHaveUniqueItems();
        shards.Count.Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public void Resolve_RangePredicateOnHashPartition_BroadcastsToAllShards()
    {
        var resolver = new ShardTargetResolver(EightShardHashMap());
        var predicate = new ShardKeyPredicate(ShardPredicateType.Range, null, "100", "200", null);
        var shards = resolver.Resolve("orders", predicate);
        shards.Should().HaveCount(8);
    }

    [Fact]
    public void Resolve_EqualityOnRangePartition_TargetsSingleShard()
    {
        var resolver = new ShardTargetResolver(FourShardRangeMap());
        var predicate = new ShardKeyPredicate(ShardPredicateType.Equality, "30000", null, null, null);
        var shards = resolver.Resolve("products", predicate);
        shards.Should().ContainSingle().Which.Should().Be(1);
    }

    [Fact]
    public void Resolve_RangeOnRangePartition_TargetsOverlappingShards()
    {
        var resolver = new ShardTargetResolver(FourShardRangeMap());
        // Range 20000-30000 overlaps shards 0 (1-25000) and 1 (25001-50000)
        var predicate = new ShardKeyPredicate(ShardPredicateType.Range, null, "20000", "30000", null);
        var shards = resolver.Resolve("products", predicate);
        shards.Should().Contain(0).And.Contain(1);
    }

    [Fact]
    public void Resolve_UnknownTable_ThrowsShardConfigurationException()
    {
        var resolver = new ShardTargetResolver(EightShardHashMap());
        var act = () => resolver.Resolve("unknown_table", predicate: null);
        act.Should().Throw<ShardConfigurationException>()
            .Which.TableName.Should().Be("unknown_table");
    }
}
