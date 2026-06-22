using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using FluentAssertions;
using NSubstitute;

namespace DistributedQuery.UnitTests.Api;

public sealed class AdminControllerTests : IClassFixture<AdminApiWebApplicationFactory>
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly AdminApiWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AdminControllerTests(AdminApiWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetStats_ReturnsDashboardStats()
    {
        var expected = new AdminDashboardStats(2, 3, 3, 10, 5, 1, DateTimeOffset.UtcNow);
        _factory.CoordinatorClient.GetAdminDashboardAsync(Arg.Any<CancellationToken>()).Returns(expected);
        _factory.QueryCacheAdmin.GetStatsAsync(Arg.Any<CancellationToken>())
            .Returns(new AdminCacheStats(10, 5, 1, DateTimeOffset.UtcNow));

        var response = await _client.GetAsync("/admin/stats");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<AdminDashboardStats>(ApiJsonOptions);
        payload!.ActiveQueries.Should().Be(2);
        payload.PlanCacheEntries.Should().Be(10);
    }

    [Fact]
    public async Task PostCacheFlush_ReturnsDeletedCount()
    {
        _factory.QueryCacheAdmin.FlushPlansAsync(null, Arg.Any<CancellationToken>())
            .Returns(new AdminCacheFlushResult(4, "all_plans", DateTimeOffset.UtcNow));

        var response = await _client.PostAsJsonAsync("/admin/cache/flush", new AdminCacheFlushRequest());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<AdminCacheFlushResult>(ApiJsonOptions);
        payload!.DeletedPlanEntries.Should().Be(4);
    }

    [Fact]
    public async Task PostCacheFlush_ReturnsBadRequest_ForInvalidPlanHash()
    {
        _factory.QueryCacheAdmin
            .When(x => x.FlushPlansAsync("not-a-hash", Arg.Any<CancellationToken>()))
            .Do(_ => throw new ArgumentException("Plan hash must be a 64-character hexadecimal SHA256 value."));

        var response = await _client.PostAsJsonAsync("/admin/cache/flush", new AdminCacheFlushRequest("not-a-hash"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetActiveQueries_ReturnsCoordinatorPage()
    {
        var queryId = Guid.NewGuid();
        var page = new ActiveQueryPage(
            [new ActiveQuerySummary(queryId, ActiveQueryKind.Sync, "abc", 2, DateTimeOffset.UtcNow, false)],
            1,
            50,
            0);

        _factory.CoordinatorClient.GetActiveQueriesAsync(50, 0, Arg.Any<CancellationToken>()).Returns(page);

        var response = await _client.GetAsync("/admin/queries/active");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"Sync\"");
        var payload = await response.Content.ReadFromJsonAsync<ActiveQueryPage>(ApiJsonOptions);
        payload!.Queries.Should().ContainSingle(q => q.QueryId == queryId);
    }

    [Fact]
    public async Task PostCancelQuery_ReturnsCancelResult()
    {
        var queryId = Guid.NewGuid();
        _factory.CoordinatorClient.CancelQueryAsync(queryId, Arg.Any<CancellationToken>())
            .Returns(new CancelQueryResult(queryId, true, true, "Cancellation requested."));

        var response = await _client.PostAsync($"/admin/queries/{queryId:D}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<CancelQueryResult>(ApiJsonOptions);
        payload!.CancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task GetWorkers_ReturnsWorkerHealthDashboard()
    {
        var dashboard = new WorkerHealthDashboard(
            [new WorkerHealthEntry(
                "worker-1",
                "127.0.0.1",
                5100,
                5101,
                [0, 1],
                "1.0.0",
                WorkerProbeStatus.Healthy,
                WorkerProbeStatus.Healthy,
                WorkerProbeStatus.Healthy,
                5,
                6,
                7,
                true)],
            1,
            1,
            DateTimeOffset.UtcNow);

        _factory.CoordinatorClient.GetWorkerHealthAsync(Arg.Any<CancellationToken>()).Returns(dashboard);

        var response = await _client.GetAsync("/admin/workers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"Healthy\"");
        var payload = await response.Content.ReadFromJsonAsync<WorkerHealthDashboard>(ApiJsonOptions);
        payload!.HealthyCount.Should().Be(1);
    }
}
