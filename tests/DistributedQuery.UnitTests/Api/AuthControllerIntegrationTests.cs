using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DistributedQuery.Api;
using DistributedQuery.Api.Contracts;
using DistributedQuery.Core.Exceptions;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using DistributedQuery.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace DistributedQuery.UnitTests.Api;

public sealed class AuthControllerIntegrationTests
{
    [Fact]
    public async Task Register_WithExistingEmail_ReturnsConflict()
    {
        await using var factory = new AuthApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var firstResponse = await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = "duplicate@example.com",
            Password = "correct-horse-battery-staple",
            DisplayName = "First User",
        });

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var duplicateResponse = await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = "duplicate@example.com",
            Password = "another-strong-password",
            DisplayName = "Second User",
        });

        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await duplicateResponse.Content.ReadFromJsonAsync<ErrorResponse>();
        body!.Error.Should().Be("email_already_exists");
        body.Message.Should().Contain("already exists");
    }

    [Fact]
    public async Task Register_SetsAccessTokenCookie_AllowsAccountProfileWithoutBearerHeader()
    {
        await using var factory = new AuthApiWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        var registerResponse = await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = "cookie@example.com",
            Password = "correct-horse-battery-staple",
            DisplayName = "Cookie User",
        });

        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        registerResponse.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders).Should().BeTrue();
        setCookieHeaders!.Any(header => header.StartsWith($"{AuthAccessTokenCookie.CookieName}=", StringComparison.Ordinal))
            .Should()
            .BeTrue();

        client.DefaultRequestHeaders.Authorization = null;

        var profileResponse = await client.GetAsync("/auth/account");
        profileResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await profileResponse.Content.ReadFromJsonAsync<UserProfileResponse>();
        profile!.Email.Should().Be("cookie@example.com");
    }

    [Fact]
    public async Task RegisterAndLogin_ReturnAccessToken()
    {
        await using var factory = new AuthApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var registerResponse = await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = "reader@example.com",
            Password = "correct-horse-battery-staple",
            DisplayName = "Reader",
        });

        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<AuthTokenResponse>();
        registerBody!.AccessToken.Should().NotBeNullOrWhiteSpace();

        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Email = "reader@example.com",
            Password = "correct-horse-battery-staple",
        });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<AuthTokenResponse>();
        loginBody!.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TokenExchange_ReturnsAccessToken()
    {
        await using var factory = new AuthApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var user = UserAccount.CreateEmailUser(
            "user-1",
            "reader@example.com",
            "Reader",
            "hash",
            ["query:read"],
            DateTimeOffset.UtcNow);
        await factory.Services.GetRequiredService<IUserRepository>().CreateAsync(user);

        var exchangeCode = await factory.Services
            .GetRequiredService<IAuthExchangeCodeStore>()
            .CreateExchangeCodeAsync("user-1", TimeSpan.FromMinutes(2));

        var response = await client.PostAsJsonAsync("/auth/token/exchange", new ExchangeTokenRequest
        {
            ExchangeCode = exchangeCode,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthTokenResponse>();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task JwksEndpoint_ReturnsSigningKeyMetadata()
    {
        await using var factory = new AuthApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/.well-known/jwks");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("dqee-signing-key");
        body.Should().Contain("\"kty\":\"RSA\"");
    }

    [Fact]
    public async Task GoogleLogin_RedirectsToGoogleAuthorizeEndpoint()
    {
        await using var factory = new AuthApiWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/auth/google/login?returnTo=%2Fquery");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.AbsoluteUri.Should().StartWith("https://accounts.google.com/o/oauth2/v2/auth");
    }

    [Fact]
    public async Task AccountProfile_GetUpdateChangePasswordAndDelete_WorkForAuthenticatedUser()
    {
        await using var factory = new AuthApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var registerResponse = await client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Email = "profile@example.com",
            Password = "correct-horse-battery-staple",
            DisplayName = "Profile User",
        });

        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<AuthTokenResponse>();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", registerBody!.AccessToken);

        var profileResponse = await client.GetAsync("/auth/account");
        profileResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await profileResponse.Content.ReadFromJsonAsync<UserProfileResponse>();
        profile!.Email.Should().Be("profile@example.com");
        profile.DisplayName.Should().Be("Profile User");
        profile.HasPasswordLogin.Should().BeTrue();

        var updateResponse = await client.PatchAsJsonAsync("/auth/account", new UpdateProfileRequest
        {
            DisplayName = "Updated Profile User",
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updateBody = await updateResponse.Content.ReadFromJsonAsync<UpdateProfileResponse>();
        updateBody!.Profile.DisplayName.Should().Be("Updated Profile User");
        updateBody.Token.Should().BeNull();

        var changePasswordResponse = await client.PostAsJsonAsync("/auth/account/change-password", new ChangePasswordRequest
        {
            CurrentPassword = "correct-horse-battery-staple",
            NewPassword = "another-strong-password",
        });

        changePasswordResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var changePasswordBody = await changePasswordResponse.Content.ReadFromJsonAsync<AuthTokenResponse>();
        changePasswordBody!.AccessToken.Should().NotBeNullOrWhiteSpace();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", changePasswordBody.AccessToken);

        var deleteResponse = await client.DeleteAsync("/auth/account");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Email = "profile@example.com",
            Password = "another-strong-password",
        });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AccountProfile_WithoutToken_ReturnsUnauthorized()
    {
        await using var factory = new AuthApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/auth/account");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed class AuthApiWebApplicationFactory : WebApplicationFactory<Program>
    {
        public IQueryCoordinatorClient CoordinatorClient { get; } = Substitute.For<IQueryCoordinatorClient>();

        public IQueryCache QueryCache { get; } = Substitute.For<IQueryCache>();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Authentication:Enabled", "true");
            builder.UseSetting("Authentication:UseLocalJwtIssuer", "true");
            builder.UseSetting("Authentication:JwtSigning:Issuer", "http://localhost/");
            builder.UseSetting("Authentication:JwtSigning:Audience", "dqee-api");
            builder.UseSetting("Authentication:Google:Enabled", "true");
            builder.UseSetting("Authentication:Google:ClientId", "google-client");
            builder.UseSetting("Authentication:Google:ClientSecret", "google-secret");
            builder.UseSetting("Authentication:GitHub:Enabled", "true");
            builder.UseSetting("Authentication:GitHub:ClientId", "github-client");
            builder.UseSetting("Authentication:GitHub:ClientSecret", "github-secret");
            builder.UseSetting("Authentication:Frontend:CallbackUrl", "http://localhost:5173/auth/callback");
            builder.UseSetting("Redis:ConnectionString", "localhost:6379,abortConnect=false,connectTimeout=100");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IQueryCoordinatorClient>();
                services.AddSingleton(CoordinatorClient);

                services.RemoveAll<IQueryCache>();
                services.AddSingleton(QueryCache);

                services.RemoveAll<IUserRepository>();
                services.AddSingleton<IUserRepository, InMemoryUserRepository>();

                services.RemoveAll<IOAuthStateStore>();
                services.AddSingleton<IOAuthStateStore, InMemoryOAuthStateStore>();

                services.RemoveAll<IAuthExchangeCodeStore>();
                services.AddSingleton<IAuthExchangeCodeStore, InMemoryAuthExchangeCodeStore>();
            });
        }
    }

    private sealed class InMemoryUserRepository : IUserRepository
    {
        private readonly Dictionary<string, UserAccount> _usersById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _usersByEmail = new(StringComparer.OrdinalIgnoreCase);

        public Task CreateAsync(UserAccount user, CancellationToken cancellationToken = default)
        {
            _usersById[user.UserId] = user;
            _usersByEmail[user.Email] = user.UserId;
            return Task.CompletedTask;
        }

        public Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_usersByEmail.TryGetValue(email, out var userId) ? _usersById.GetValueOrDefault(userId) : null);
        }

        public Task<UserAccount?> FindByExternalLoginAsync(AuthProviderKind provider, string providerKey, CancellationToken cancellationToken = default)
        {
            var match = _usersById.Values.FirstOrDefault(user =>
                user.ExternalLogins.Any(login =>
                    login.Provider.Equals(provider.ToString(), StringComparison.OrdinalIgnoreCase) &&
                    login.ProviderKey == providerKey));

            return Task.FromResult(match);
        }

        public Task<UserAccount?> FindByIdAsync(string userId, CancellationToken cancellationToken = default)
        {
            _usersById.TryGetValue(userId, out var user);
            return Task.FromResult(user);
        }

        public Task UpdateAsync(UserAccount user, CancellationToken cancellationToken = default)
        {
            if (_usersById.TryGetValue(user.UserId, out var existing) &&
                !string.Equals(existing.Email, user.Email, StringComparison.OrdinalIgnoreCase))
            {
                _usersByEmail.Remove(existing.Email);
            }

            _usersById[user.UserId] = user;
            _usersByEmail[user.Email] = user.UserId;
            return Task.CompletedTask;
        }

        public Task<bool> SoftDeleteAsync(string userId, CancellationToken cancellationToken = default)
        {
            if (!_usersById.TryGetValue(userId, out var user) || user.IsDeleted)
            {
                return Task.FromResult(false);
            }

            var deleted = user.SoftDelete(DateTimeOffset.UtcNow);
            _usersById[userId] = deleted;
            return Task.FromResult(true);
        }
    }

    private sealed class InMemoryOAuthStateStore : IOAuthStateStore
    {
        private readonly Dictionary<string, OAuthAuthorizationRequest> _states = new(StringComparer.Ordinal);

        public Task<OAuthAuthorizationRequest?> ConsumeAuthorizationRequestAsync(
            AuthProviderKind provider,
            string state,
            CancellationToken cancellationToken = default)
        {
            var key = $"{provider}:{state}";
            if (!_states.TryGetValue(key, out var request))
            {
                return Task.FromResult<OAuthAuthorizationRequest?>(null);
            }

            _states.Remove(key);
            return Task.FromResult<OAuthAuthorizationRequest?>(request);
        }

        public Task StoreAuthorizationRequestAsync(OAuthAuthorizationRequest request, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            _states[$"{request.Provider}:{request.State}"] = request;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryAuthExchangeCodeStore : IAuthExchangeCodeStore
    {
        private readonly Dictionary<string, string> _codes = new(StringComparer.Ordinal);

        public Task<string> CreateExchangeCodeAsync(string userId, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            var code = Guid.NewGuid().ToString("N");
            _codes[code] = userId;
            return Task.FromResult(code);
        }

        public Task<string?> ConsumeExchangeCodeAsync(string exchangeCode, CancellationToken cancellationToken = default)
        {
            if (!_codes.TryGetValue(exchangeCode, out var userId))
            {
                return Task.FromResult<string?>(null);
            }

            _codes.Remove(exchangeCode);
            return Task.FromResult<string?>(userId);
        }
    }
}
