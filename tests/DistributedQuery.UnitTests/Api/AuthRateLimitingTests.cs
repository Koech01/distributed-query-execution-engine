using System.Net;
using System.Net.Http.Json;
using DistributedQuery.Api.Contracts;
using FluentAssertions;

namespace DistributedQuery.UnitTests.Api;

public sealed class AuthRateLimitingTests : IClassFixture<AuthRateLimitWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthRateLimitingTests(AuthRateLimitWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_Returns429_WhenIpRateLimitExceeded()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var response = await _client.PostAsJsonAsync("/auth/login", new LoginRequest
            {
                Email = $"user{attempt}@example.com",
                Password = "wrong-password-value"
            });

            response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }

        var limited = await _client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Email = "limited@example.com",
            Password = "wrong-password-value"
        });

        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
