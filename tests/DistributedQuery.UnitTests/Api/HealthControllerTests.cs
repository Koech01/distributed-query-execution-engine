using System.Net;
using FluentAssertions;

namespace DistributedQuery.UnitTests.Api;

public sealed class HealthControllerTests : IClassFixture<QueryApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthControllerTests(QueryApiWebApplicationFactory factory) =>
        _client = factory.CreateClient();

    [Fact]
    public async Task GetLive_ReturnsOk()
    {
        var response = await _client.GetAsync("/health/live");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
