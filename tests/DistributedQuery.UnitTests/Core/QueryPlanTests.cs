using DistributedQuery.Core.Models;
using FluentAssertions;

namespace DistributedQuery.UnitTests.Core;

public class QueryPlanTests
{
    [Fact]
    public void Create_AssignsNewPlanId()
    {
        var a = QueryPlan.Create("hash1", [], MergeInstructions.Empty);
        var b = QueryPlan.Create("hash1", [], MergeInstructions.Empty);

        a.PlanId.Should().NotBe(Guid.Empty);
        a.PlanId.Should().NotBe(b.PlanId);
    }

    [Fact]
    public void Create_StoresPlanHash()
    {
        var plan = QueryPlan.Create("abc123def456", [], MergeInstructions.Empty);

        plan.PlanHash.Should().Be("abc123def456");
    }

    [Fact]
    public void Create_SetsCreatedAtToUtcNow()
    {
        var before = DateTimeOffset.UtcNow;
        var plan = QueryPlan.Create("hash", [], MergeInstructions.Empty);
        var after = DateTimeOffset.UtcNow;

        plan.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Create_StoresSubQueries()
    {
        var sub1 = SubQuery.Create(Guid.NewGuid(), "SELECT 1", "node-01", 0, 2);
        var sub2 = SubQuery.Create(Guid.NewGuid(), "SELECT 1", "node-02", 1, 2);

        var plan = QueryPlan.Create("hash", [sub1, sub2], MergeInstructions.Empty);

        plan.SubQueries.Should().HaveCount(2);
        plan.SubQueries.Should().Contain(sub1).And.Contain(sub2);
    }

    [Fact]
    public void Create_StoresMergeInstructions()
    {
        var instructions = new MergeInstructions(
            OrderBy: [new OrderByColumn("amount", Descending: true)],
            Aggregates: [],
            Limit: 10,
            Offset: null,
            IsDistinct: false);

        var plan = QueryPlan.Create("hash", [], instructions);

        plan.MergeInstructions.Should().Be(instructions);
    }
}

public class MergeInstructionsTests
{
    [Fact]
    public void Empty_HasNoOrderBy()
    {
        MergeInstructions.Empty.OrderBy.Should().BeEmpty();
    }

    [Fact]
    public void Empty_HasNoAggregates()
    {
        MergeInstructions.Empty.Aggregates.Should().BeEmpty();
    }

    [Fact]
    public void Empty_HasNullLimitAndOffset()
    {
        MergeInstructions.Empty.Limit.Should().BeNull();
        MergeInstructions.Empty.Offset.Should().BeNull();
    }

    [Fact]
    public void Empty_IsDistinctFalse()
    {
        MergeInstructions.Empty.IsDistinct.Should().BeFalse();
    }

    [Fact]
    public void Empty_ReturnsSameInstance()
    {
        MergeInstructions.Empty.Should().BeSameAs(MergeInstructions.Empty);
    }

    [Fact]
    public void OrderByColumn_StoresColumnNameAndDirection()
    {
        var col = new OrderByColumn("amount", Descending: true);

        col.ColumnName.Should().Be("amount");
        col.Descending.Should().BeTrue();
    }

    [Fact]
    public void AggregateOperation_StoresAllFields()
    {
        var op = new AggregateOperation(AggregateFunction.Sum, "amount", "__sum_amount");

        op.Function.Should().Be(AggregateFunction.Sum);
        op.SourceColumn.Should().Be("amount");
        op.OutputAlias.Should().Be("__sum_amount");
    }

    [Theory]
    [InlineData(AggregateFunction.Sum)]
    [InlineData(AggregateFunction.Count)]
    [InlineData(AggregateFunction.Avg)]
    [InlineData(AggregateFunction.Min)]
    [InlineData(AggregateFunction.Max)]
    [InlineData(AggregateFunction.CountDistinct)]
    public void AggregateFunction_AllValuesAreDefined(AggregateFunction function)
    {
        Enum.IsDefined(function).Should().BeTrue();
    }
}
