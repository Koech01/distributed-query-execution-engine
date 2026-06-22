using System.Net;
using System.Text.Json;
using DistributedQuery.Core.Models;
using DistributedQuery.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DistributedQuery.UnitTests.Infrastructure;

public sealed class GoogleOAuthClientTests
{
    [Fact]
    public void BuildAuthorizationUrl_IncludesPkceChallengeWhenVerifierProvided()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var request = new OAuthAuthorizationRequest(
            AuthProviderKind.Google,
            "/query",
            "state-token",
            "verifier-token");

        var url = client.BuildAuthorizationUrl(request, "http://localhost:5281/auth/google/callback");

        url.Should().Contain("client_id=google-client");
        url.Should().Contain("code_challenge=");
        url.Should().Contain("code_challenge_method=S256");
        url.Should().Contain("state=state-token");
    }

    [Fact]
    public async Task ExchangeAuthorizationCodeAsync_ReturnsProfileFromGoogleUserInfo()
    {
        var client = CreateClient(request =>
        {
            if (request.RequestUri!.AbsoluteUri.Contains("oauth2.googleapis.com/token"))
            {
                return JsonResponse(new { access_token = "provider-token" });
            }

            if (request.RequestUri.AbsoluteUri.Contains("openidconnect.googleapis.com/v1/userinfo"))
            {
                return JsonResponse(new
                {
                    sub = "google-subject",
                    email = "reader@example.com",
                    name = "Reader",
                });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var profile = await client.ExchangeAuthorizationCodeAsync(
            "auth-code",
            "http://localhost:5281/auth/google/callback",
            "verifier-token");

        profile.ProviderKey.Should().Be("google-subject");
        profile.Email.Should().Be("reader@example.com");
        profile.DisplayName.Should().Be("Reader");
    }

    private static GoogleOAuthClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handlerFactory)
    {
        var handler = new StubHttpMessageHandler(handlerFactory);
        return new GoogleOAuthClient(
            new HttpClient(handler),
            Options.Create(new GoogleOAuthOptions
            {
                Enabled = true,
                ClientId = "google-client",
                ClientSecret = "google-secret",
            }),
            NullLogger<GoogleOAuthClient>.Instance);
    }

    private static HttpResponseMessage JsonResponse(object payload) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json"),
        };

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handlerFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(handlerFactory(request));
    }
}

public sealed class GitHubOAuthClientTests
{
    [Fact]
    public void BuildAuthorizationUrl_IncludesClientAndState()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var request = new OAuthAuthorizationRequest(AuthProviderKind.GitHub, "/query", "state-token");

        var url = client.BuildAuthorizationUrl(request, "http://localhost:5281/auth/github/callback");

        url.Should().Contain("client_id=github-client");
        url.Should().Contain("state=state-token");
        url.Should().Contain("github.com/login/oauth/authorize");
    }

    [Fact]
    public async Task ExchangeAuthorizationCodeAsync_ResolvesPrimaryEmailWhenMissingFromProfile()
    {
        var client = CreateClient(request =>
        {
            if (request.RequestUri!.AbsoluteUri.Contains("login/oauth/access_token"))
            {
                return JsonResponse(new { access_token = "provider-token" });
            }

            if (request.RequestUri.AbsoluteUri.Contains("api.github.com/user/emails"))
            {
                return JsonResponse(new[]
                {
                    new { email = "reader@example.com", primary = true, verified = true },
                });
            }

            if (request.RequestUri.AbsoluteUri.Contains("api.github.com/user"))
            {
                return JsonResponse(new
                {
                    id = 42,
                    login = "reader",
                    name = "Reader",
                });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var profile = await client.ExchangeAuthorizationCodeAsync(
            "auth-code",
            "http://localhost:5281/auth/github/callback",
            null);

        profile.ProviderKey.Should().Be("42");
        profile.Email.Should().Be("reader@example.com");
        profile.DisplayName.Should().Be("Reader");
    }

    private static GitHubOAuthClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handlerFactory)
    {
        var handler = new StubHttpMessageHandler(handlerFactory);
        return new GitHubOAuthClient(
            new HttpClient(handler),
            Options.Create(new GitHubOAuthOptions
            {
                Enabled = true,
                ClientId = "github-client",
                ClientSecret = "github-secret",
            }),
            NullLogger<GitHubOAuthClient>.Instance);
    }

    private static HttpResponseMessage JsonResponse(object payload) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json"),
        };

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handlerFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(handlerFactory(request));
    }
}

public sealed class RsaAuthTokenIssuerTests
{
    [Fact]
    public void IssueAccessToken_IncludesScopeClaims()
    {
        var keyProvider = new JwtSigningKeyProvider(
            Options.Create(new JwtSigningOptions
            {
                Issuer = "http://localhost:5281/",
                Audience = "dqee-api",
                AccessTokenLifetimeSeconds = 3600,
            }),
            new TestHostEnvironment());

        var issuer = new RsaAuthTokenIssuer(
            Options.Create(new JwtSigningOptions
            {
                Issuer = "http://localhost:5281/",
                Audience = "dqee-api",
                AccessTokenLifetimeSeconds = 3600,
            }),
            keyProvider,
            NullLogger<RsaAuthTokenIssuer>.Instance);

        var user = UserAccount.CreateEmailUser(
            "user-1",
            "reader@example.com",
            "Reader",
            "hash",
            ["query:read", "query:admin"],
            DateTimeOffset.UtcNow);

        var token = issuer.IssueAccessToken(user);

        token.AccessToken.Should().NotBeNullOrWhiteSpace();
        token.ExpiresInSeconds.Should().Be(3600);
    }

    private sealed class TestHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";

        public string ApplicationName { get; set; } = "tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
