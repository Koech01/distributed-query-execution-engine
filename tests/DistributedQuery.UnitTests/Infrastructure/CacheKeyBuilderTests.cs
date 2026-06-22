using DistributedQuery.Core.Models;
using DistributedQuery.Infrastructure.Caching;
using FluentAssertions;

namespace DistributedQuery.UnitTests.Infrastructure;

public class CacheKeyBuilderTests
{
    // ForPlan - key structure

    [Fact]
    public void ForPlan_ReturnsPlanPrefixedKey()
    {
        var key = CacheKeyBuilder.ForPlan("SELECT 1", []);

        key.Should().StartWith("plan::");
    }

    [Fact]
    public void ForPlan_ProducesFixedLengthHexHash()
    {
        var key = CacheKeyBuilder.ForPlan("SELECT * FROM orders", []);
        var hash = key["plan::".Length..];

        // SHA256 = 32 bytes = 64 hex chars
        hash.Should().HaveLength(64);
        hash.Should().MatchRegex("^[0-9a-f]+$");
    }

    [Fact]
    public void ForPlan_SameSqlSameParams_ProducesSameKey()
    {
        var params1 = new List<QueryParameter> { new("@id", "int", "1") };
        var params2 = new List<QueryParameter> { new("@id", "int", "99") };

        var key1 = CacheKeyBuilder.ForPlan("SELECT * FROM orders WHERE id = @id", params1);
        var key2 = CacheKeyBuilder.ForPlan("SELECT * FROM orders WHERE id = @id", params2);

        // Parameter values must NOT affect the plan cache key - only types matter
        key1.Should().Be(key2);
    }

    [Fact]
    public void ForPlan_DifferentParamTypes_ProduceDifferentKeys()
    {
        var intParam    = new List<QueryParameter> { new("@id", "int", "1") };
        var stringParam = new List<QueryParameter> { new("@id", "nvarchar", "1") };

        var key1 = CacheKeyBuilder.ForPlan("SELECT * FROM orders WHERE id = @id", intParam);
        var key2 = CacheKeyBuilder.ForPlan("SELECT * FROM orders WHERE id = @id", stringParam);

        key1.Should().NotBe(key2);
    }

    [Fact]
    public void ForPlan_DifferentSql_ProduceDifferentKeys()
    {
        var key1 = CacheKeyBuilder.ForPlan("SELECT * FROM orders", []);
        var key2 = CacheKeyBuilder.ForPlan("SELECT * FROM products", []);

        key1.Should().NotBe(key2);
    }

    // Normalization - whitespace and casing must not affect the key

    [Fact]
    public void ForPlan_WhitespaceDifferences_ProduceSameKey()
    {
        var key1 = CacheKeyBuilder.ForPlan("SELECT * FROM orders WHERE id = @id", []);
        var key2 = CacheKeyBuilder.ForPlan("  SELECT  *  FROM  orders  WHERE  id  =  @id  ", []);

        key1.Should().Be(key2);
    }

    [Fact]
    public void ForPlan_KeywordCaseDifferences_ProduceSameKey()
    {
        var key1 = CacheKeyBuilder.ForPlan("SELECT * FROM orders", []);
        var key2 = CacheKeyBuilder.ForPlan("select * from orders", []);
        var key3 = CacheKeyBuilder.ForPlan("Select * From Orders", []);

        key1.Should().Be(key2);
        key2.Should().Be(key3);
    }

    // Parameter ordering must not affect the key

    [Fact]
    public void ForPlan_ParameterOrderIndependent_ProduceSameKey()
    {
        var paramsAB = new List<QueryParameter>
        {
            new("@a", "int", "1"),
            new("@b", "nvarchar", "x")
        };
        var paramsBA = new List<QueryParameter>
        {
            new("@b", "nvarchar", "x"),
            new("@a", "int", "1")
        };

        var key1 = CacheKeyBuilder.ForPlan("SELECT 1", paramsAB);
        var key2 = CacheKeyBuilder.ForPlan("SELECT 1", paramsBA);

        key1.Should().Be(key2);
    }

    [Fact]
    public void ForPlan_ParamNameCaseDifferences_ProduceSameKey()
    {
        var lower = new List<QueryParameter> { new("@id", "int", "1") };
        var upper = new List<QueryParameter> { new("@ID", "int", "1") };

        CacheKeyBuilder.ForPlan("SELECT 1", lower)
            .Should().Be(CacheKeyBuilder.ForPlan("SELECT 1", upper));
    }

    // ForResult - key structure

    [Fact]
    public void ForResult_ReturnsResultPrefixedKey()
    {
        var key = CacheKeyBuilder.ForResult(Guid.NewGuid());

        key.Should().StartWith("result::");
    }

    [Fact]
    public void ForResult_SameQueryId_ProducesSameKey()
    {
        var queryId = Guid.NewGuid();

        CacheKeyBuilder.ForResult(queryId).Should().Be(CacheKeyBuilder.ForResult(queryId));
    }

    [Fact]
    public void ForResult_DifferentQueryIds_ProduceDifferentKeys()
    {
        var key1 = CacheKeyBuilder.ForResult(Guid.NewGuid());
        var key2 = CacheKeyBuilder.ForResult(Guid.NewGuid());

        key1.Should().NotBe(key2);
    }

    [Fact]
    public void ForResult_KeyContainsQueryIdWithoutHyphens()
    {
        var queryId = Guid.NewGuid();
        var key     = CacheKeyBuilder.ForResult(queryId);

        key.Should().Contain(queryId.ToString("N"));
        key.Should().NotContain("-");
    }

    // NormalizeSql internal behaviour

    [Fact]
    public void NormalizeSql_TrimsLeadingAndTrailingWhitespace()
    {
        var result = CacheKeyBuilder.NormalizeSql("  SELECT 1  ");

        result.Should().Be("SELECT 1");
    }

    [Fact]
    public void NormalizeSql_CollapsesInternalWhitespace()
    {
        var result = CacheKeyBuilder.NormalizeSql("SELECT   *   FROM   orders");

        result.Should().Be("SELECT * FROM ORDERS");
    }

    [Fact]
    public void NormalizeSql_UppercasesAllCharacters()
    {
        var result = CacheKeyBuilder.NormalizeSql("select id from orders");

        result.Should().Be("SELECT ID FROM ORDERS");
    }

    [Fact]
    public void NormalizeSql_TabsAndNewlines_TreatedAsWhitespace()
    {
        var result = CacheKeyBuilder.NormalizeSql("SELECT\t*\nFROM\r\norders");

        result.Should().Be("SELECT * FROM ORDERS");
    }
}
