using DistributedQuery.Core.Models;
using FluentAssertions;

namespace DistributedQuery.UnitTests.Core;

public class NodeInfoTests
{
    [Fact]
    public void NodeInfo_StoresAllFields()
    {
        var node = new NodeInfo("worker-01", "10.0.1.42", 5100, [0, 1, 2], "1.2.0");

        node.NodeId.Should().Be("worker-01");
        node.Address.Should().Be("10.0.1.42");
        node.GrpcPort.Should().Be(5100);
        node.Shards.Should().BeEquivalentTo([0, 1, 2]);
        node.Version.Should().Be("1.2.0");
    }

    [Fact]
    public void NodeInfo_WithSingleShard_StoresShard()
    {
        var node = new NodeInfo("worker-02", "10.0.1.43", 5100, [3], "1.0.0");

        node.Shards.Should().ContainSingle().Which.Should().Be(3);
    }

    [Fact]
    public void NodeInfo_EqualityByValue()
    {
        var a = new NodeInfo("worker-01", "10.0.1.42", 5100, [0, 1], "1.0.0");
        var b = new NodeInfo("worker-01", "10.0.1.42", 5100, [0, 1], "1.0.0");

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}

public class ShardDescriptorTests
{
    [Fact]
    public void ShardDescriptor_StoresAllFields()
    {
        var shard = new ShardDescriptor(2, 8, "customer_id", "orders");

        shard.ShardIndex.Should().Be(2);
        shard.TotalShards.Should().Be(8);
        shard.ShardKey.Should().Be("customer_id");
        shard.TableName.Should().Be("orders");
    }

    [Fact]
    public void ShardDescriptor_EqualityByValue()
    {
        var a = new ShardDescriptor(0, 4, "id", "products");
        var b = new ShardDescriptor(0, 4, "id", "products");

        a.Should().Be(b);
    }
}
