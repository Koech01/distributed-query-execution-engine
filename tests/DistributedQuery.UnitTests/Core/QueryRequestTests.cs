using DistributedQuery.Core.Models;
using FluentAssertions;

namespace DistributedQuery.UnitTests.Core;

public class QueryRequestTests
{
    [Fact]
    public void Create_AssignsNewQueryId()
    {
        var a = QueryRequest.Create("SELECT 1");
        var b = QueryRequest.Create("SELECT 1");

        a.QueryId.Should().NotBe(Guid.Empty);
        a.QueryId.Should().NotBe(b.QueryId);
    }

    [Fact]
    public void Create_DefaultsToThirtySecondTimeout()
    {
        var request = QueryRequest.Create("SELECT 1");
        request.Timeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Create_WithoutParameters_HasEmptyList()
    {
        var request = QueryRequest.Create("SELECT 1");
        request.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithParameters_StoresParameters()
    {
        var parameters = new List<QueryParameter> { new("@id", "int", "42") };
        var request = QueryRequest.Create("SELECT * FROM orders WHERE id = @id", parameters);

        request.Parameters.Should().ContainSingle()
            .Which.Name.Should().Be("@id");
    }

    [Fact]
    public void Create_StoresSqlExactly()
    {
        const string sql = "SELECT id, name FROM orders WHERE status = 'active'";
        var request = QueryRequest.Create(sql);
        request.Sql.Should().Be(sql);
    }

    [Fact]
    public void Create_DefaultsToAsyncFalse()
    {
        var request = QueryRequest.Create("SELECT 1");
        request.Async.Should().BeFalse();
    }

    [Fact]
    public void Create_DefaultsMaxNodesToNull()
    {
        var request = QueryRequest.Create("SELECT 1");
        request.MaxNodes.Should().BeNull();
    }

    [Fact]
    public void Create_DefaultsToFailurePolicyBestEffort()
    {
        var request = QueryRequest.Create("SELECT 1");
        request.FailurePolicy.Should().Be(FailurePolicy.BestEffort);
    }
}
