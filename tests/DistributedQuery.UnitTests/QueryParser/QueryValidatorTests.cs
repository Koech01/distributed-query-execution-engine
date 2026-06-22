using DistributedQuery.Core.Exceptions;
using DistributedQuery.QueryParser.Parsing;
using FluentAssertions;

namespace DistributedQuery.UnitTests.QueryParser;

public sealed class QueryValidatorTests
{
    [Fact]
    public void Validate_ValidSelect_DoesNotThrow()
    {
        var act = () => QueryValidator.Validate("SELECT id, name FROM orders WHERE customer_id = 1");
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("INSERT INTO orders VALUES (1)")]
    [InlineData("UPDATE orders SET name = 'x'")]
    [InlineData("DELETE FROM orders")]
    [InlineData("DROP TABLE orders")]
    [InlineData("CREATE TABLE foo (id INT)")]
    public void Validate_NonSelectStatement_ThrowsQueryParseException(string sql)
    {
        var act = () => QueryValidator.Validate(sql);
        act.Should().Throw<QueryParseException>()
            .Which.ParseErrors.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("SELECT xp_cmdshell('dir') FROM orders")]
    [InlineData("SELECT * FROM openrowset('provider', 'src', 'query')")]
    public void Validate_BlockedToken_ThrowsQueryParseException(string sql)
    {
        var act = () => QueryValidator.Validate(sql);
        act.Should().Throw<QueryParseException>();
    }

    [Fact]
    public void Validate_EmptySql_ThrowsQueryParseException()
    {
        var act = () => QueryValidator.Validate("   ");
        act.Should().Throw<QueryParseException>();
    }

    [Fact]
    public void Validate_SqlExceedingMaxLength_ThrowsQueryParseException()
    {
        var longSql = "SELECT * FROM orders WHERE id = " + new string('1', 10_001);
        var act = () => QueryValidator.Validate(longSql);
        act.Should().Throw<QueryParseException>()
            .Which.Message.Should().Contain("maximum length");
    }

    [Fact]
    public void Validate_MalformedSql_ThrowsQueryParseExceptionWithParseErrors()
    {
        var act = () => QueryValidator.Validate("SELECT FROM WHERE");
        act.Should().Throw<QueryParseException>()
            .Which.ParseErrors.Should().NotBeEmpty();
    }
}