using DistributedQuery.Infrastructure.Grpc;
using FluentAssertions;

namespace DistributedQuery.UnitTests.Infrastructure;

public class QueryExecutionProtoTests
{
    [Fact]
    public void GeneratedGrpcTypes_AreAvailable()
    {
        typeof(QueryExecution.QueryExecutionClient).Should().NotBeNull();
        typeof(PartialResultResponse).Should().NotBeNull();
        typeof(HealthCheckResponse).Should().NotBeNull();
    }
}
