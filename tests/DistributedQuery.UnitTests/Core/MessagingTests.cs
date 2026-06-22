using DistributedQuery.Core.Messages;
using DistributedQuery.Core.Models;
using FluentAssertions;
using Xunit;

namespace DistributedQuery.UnitTests.Core;

public class MessagingTests
{
    [Fact]
    public void SubQueryDispatched_CanBeConstructed()
    {
        var param = new QueryParameter("id", "int", "1");
        var msg = new SubQueryDispatched(
            SubQueryId: Guid.NewGuid(),
            ParentQueryId: Guid.NewGuid(),
            Sql: "SELECT * FROM users WHERE id = @id",
            ShardIndex: 0,
            TotalShards: 3,
            Parameters: new[] { param },
            TimeoutMs: 5000,
            TraceParent: null,
            TraceState: null
        );

        msg.Sql.Should().StartWith("SELECT");
        msg.Parameters.Should().ContainSingle().Which.Name.Should().Be("id");
        msg.SchemaVersion.Should().Be(1);
    }

    [Fact]
    public void PartialResultReady_CanBeConstructed()
    {
        var rows = new List<IReadOnlyList<string>> { new[] { "1", "alice" } };
        var msg = new PartialResultReady(
            SubQueryId: Guid.NewGuid(),
            ParentQueryId: Guid.NewGuid(),
            ShardIndex: 0,
            Success: true,
            ErrorMessage: null,
            ColumnNames: new[] { "id", "name" },
            Rows: rows,
            ExecutionMs: 123
        );

        msg.Success.Should().BeTrue();
        msg.ColumnNames.Should().HaveCount(2);
        msg.Rows.Should().HaveCount(1);
        msg.TotalShards.Should().Be(0);
        msg.SchemaVersion.Should().Be(1);
    }

    [Fact]
    public void QueryCompleted_CanBeConstructed()
    {
        var msg = new QueryCompleted(Guid.NewGuid(), true, null);
        msg.Success.Should().BeTrue();
        msg.SchemaVersion.Should().Be(1);
    }
}
