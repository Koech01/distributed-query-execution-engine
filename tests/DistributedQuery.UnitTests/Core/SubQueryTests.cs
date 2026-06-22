using DistributedQuery.Core.Models;
using FluentAssertions;

namespace DistributedQuery.UnitTests.Core;

public class SubQueryTests
{
    [Fact]
    public void Create_AssignsNewSubQueryId()
    {
        var parentId = Guid.NewGuid();
        var a = SubQuery.Create(parentId, "SELECT 1", "node-01", 0, 4);
        var b = SubQuery.Create(parentId, "SELECT 1", "node-01", 0, 4);

        a.SubQueryId.Should().NotBe(Guid.Empty);
        a.SubQueryId.Should().NotBe(b.SubQueryId);
    }

    [Fact]
    public void Create_BindsParentQueryId()
    {
        var parentId = Guid.NewGuid();
        var sub = SubQuery.Create(parentId, "SELECT 1", "node-01", 0, 4);

        sub.ParentQueryId.Should().Be(parentId);
    }

    [Fact]
    public void Create_WithoutParameters_HasEmptyList()
    {
        var sub = SubQuery.Create(Guid.NewGuid(), "SELECT 1", "node-01", 0, 4);

        sub.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithParameters_StoresParameters()
    {
        var param = new QueryParameter("@id", "int", "42");
        var sub = SubQuery.Create(Guid.NewGuid(), "SELECT 1", "node-01", 0, 4, [param]);

        sub.Parameters.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(param);
    }

    [Fact]
    public void Create_StoresShardIndexAndTotalShards()
    {
        var sub = SubQuery.Create(Guid.NewGuid(), "SELECT 1", "node-02", 3, 8);

        sub.ShardIndex.Should().Be(3);
        sub.TotalShards.Should().Be(8);
        sub.TargetNodeId.Should().Be("node-02");
    }

    [Fact]
    public void QueryParameter_StoresAllFields()
    {
        var param = new QueryParameter("@name", "nvarchar", "\"Alice\"");

        param.Name.Should().Be("@name");
        param.Type.Should().Be("nvarchar");
        param.Value.Should().Be("\"Alice\"");
    }
}
