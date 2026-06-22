using DistributedQuery.Core.Models;
using FluentAssertions;

namespace DistributedQuery.UnitTests.Core;

public class PartialResultTests
{
    private static readonly IReadOnlyList<ColumnDescriptor> _columns =
        [new ColumnDescriptor("id", "int", Nullable: false), new ColumnDescriptor("name", "nvarchar", Nullable: true)];

    private static readonly IReadOnlyList<IReadOnlyList<string>> _rows =
        [["1", "Alice"], ["2", "Bob"]];

    [Fact]
    public void Success_SetsStatusToSuccess()
    {
        var result = PartialResult.Success(Guid.NewGuid(), Guid.NewGuid(), 0, _columns, _rows, 15);

        result.Status.Should().Be(PartialResultStatus.Success);
    }

    [Fact]
    public void Success_IsSuccessReturnsTrue()
    {
        var result = PartialResult.Success(Guid.NewGuid(), Guid.NewGuid(), 0, _columns, _rows, 15);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Success_ErrorMessageIsNull()
    {
        var result = PartialResult.Success(Guid.NewGuid(), Guid.NewGuid(), 0, _columns, _rows, 15);

        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Success_StoresRowsAndColumns()
    {
        var result = PartialResult.Success(Guid.NewGuid(), Guid.NewGuid(), 2, _columns, _rows, 42);

        result.Columns.Should().HaveCount(2);
        result.Rows.Should().HaveCount(2);
        result.ExecutionMs.Should().Be(42);
        result.ShardIndex.Should().Be(2);
    }

    [Fact]
    public void Success_BindsParentQueryId()
    {
        var subId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var result = PartialResult.Success(subId, parentId, 0, _columns, _rows, 10);

        result.SubQueryId.Should().Be(subId);
        result.ParentQueryId.Should().Be(parentId);
    }

    [Theory]
    [InlineData(PartialResultStatus.Failed)]
    [InlineData(PartialResultStatus.TimedOut)]
    [InlineData(PartialResultStatus.Degraded)]
    public void Failure_IsSuccessReturnsFalse(PartialResultStatus status)
    {
        var result = PartialResult.Failure(Guid.NewGuid(), Guid.NewGuid(), 1, status, "error");

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(status);
    }

    [Fact]
    public void Failure_StoresErrorMessage()
    {
        var result = PartialResult.Failure(Guid.NewGuid(), Guid.NewGuid(), 1,
            PartialResultStatus.TimedOut, "Timed out after 25000ms");

        result.ErrorMessage.Should().Be("Timed out after 25000ms");
    }

    [Fact]
    public void Failure_HasEmptyRowsAndColumns()
    {
        var result = PartialResult.Failure(Guid.NewGuid(), Guid.NewGuid(), 1,
            PartialResultStatus.Failed, "DB error");

        result.Rows.Should().BeEmpty();
        result.Columns.Should().BeEmpty();
        result.ExecutionMs.Should().Be(0);
    }

    [Fact]
    public void ColumnDescriptor_StoresAllFields()
    {
        var col = new ColumnDescriptor("amount", "decimal", Nullable: false);

        col.Name.Should().Be("amount");
        col.DataType.Should().Be("decimal");
        col.Nullable.Should().BeFalse();
    }
}
