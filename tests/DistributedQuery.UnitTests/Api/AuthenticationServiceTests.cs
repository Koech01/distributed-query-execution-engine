using DistributedQuery.Api.Services;
using DistributedQuery.Core.Exceptions;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using DistributedQuery.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DistributedQuery.UnitTests.Api;

public sealed class AuthenticationServiceTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IAuthTokenIssuer _tokenIssuer = Substitute.For<IAuthTokenIssuer>();
    private readonly IOAuthStateStore _oauthStateStore = Substitute.For<IOAuthStateStore>();
    private readonly IAuthExchangeCodeStore _exchangeCodeStore = Substitute.For<IAuthExchangeCodeStore>();
    private readonly OAuthProviderRegistry _oauthProviderRegistry;

    public AuthenticationServiceTests()
    {
        _oauthProviderRegistry = new OAuthProviderRegistry(
            new GoogleOAuthClient(
                new HttpClient(),
                Options.Create(new GoogleOAuthOptions { Enabled = true, ClientId = "google-client", ClientSecret = "secret" }),
                NullLogger<GoogleOAuthClient>.Instance),
            new GitHubOAuthClient(
                new HttpClient(),
                Options.Create(new GitHubOAuthOptions { Enabled = true, ClientId = "github-client", ClientSecret = "secret" }),
                NullLogger<GitHubOAuthClient>.Instance));
    }

    [Fact]
    public async Task RegisterWithEmailAsync_CreatesUserAndIssuesToken()
    {
        _userRepository.FindByEmailAsync("reader@example.com", Arg.Any<CancellationToken>()).Returns((UserAccount?)null);
        _passwordHasher.HashPassword("correct-horse-battery-staple").Returns("hashed");
        _tokenIssuer.IssueAccessToken(Arg.Any<UserAccount>(), Arg.Any<CancellationToken>())
            .Returns(new AuthTokenResult("token", 3600));

        var service = CreateService();

        var result = await service.RegisterWithEmailAsync(
            "reader@example.com",
            "correct-horse-battery-staple",
            "Reader",
            CancellationToken.None);

        result.AccessToken.Should().Be("token");
        await _userRepository.Received(1).CreateAsync(Arg.Any<UserAccount>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginWithEmailAsync_WithInvalidPassword_ThrowsAuthenticationException()
    {
        var user = UserAccount.CreateEmailUser(
            "user-1",
            "reader@example.com",
            "Reader",
            "hashed",
            ["query:read"],
            DateTimeOffset.UtcNow);

        _userRepository.FindByEmailAsync("reader@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.VerifyPassword("wrong-password", "hashed").Returns(false);

        var service = CreateService();

        var action = () => service.LoginWithEmailAsync("reader@example.com", "wrong-password", CancellationToken.None);

        await action.Should().ThrowAsync<AuthenticationException>();
        await _userRepository.Received(1).UpdateAsync(
            Arg.Is<UserAccount>(account => account.FailedLoginAttempts == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginWithEmailAsync_AfterThreshold_LocksAccount()
    {
        var user = UserAccount.CreateEmailUser(
            "user-1",
            "reader@example.com",
            "Reader",
            "hashed",
            ["query:read"],
            DateTimeOffset.UtcNow) with { FailedLoginAttempts = 4 };

        _userRepository.FindByEmailAsync("reader@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.VerifyPassword("wrong-password", "hashed").Returns(false);

        var service = CreateService();

        var exception = await service
            .Invoking(s => s.LoginWithEmailAsync("reader@example.com", "wrong-password", CancellationToken.None))
            .Should().ThrowAsync<AuthenticationException>();

        exception.Which.Kind.Should().Be(AuthenticationFailureKind.AccountLocked);
    }

    [Fact]
    public async Task LoginWithEmailAsync_WithDeletedAccount_ThrowsAccountDeleted()
    {
        var user = UserAccount.CreateEmailUser(
            "user-1",
            "reader@example.com",
            "Reader",
            "hashed",
            ["query:read"],
            DateTimeOffset.UtcNow).SoftDelete(DateTimeOffset.UtcNow);

        _userRepository.FindByEmailAsync("reader@example.com", Arg.Any<CancellationToken>()).Returns(user);

        var service = CreateService();

        var exception = await service.Invoking(s => s.LoginWithEmailAsync("reader@example.com", "password", CancellationToken.None))
            .Should().ThrowAsync<AuthenticationException>();

        exception.Which.Kind.Should().Be(AuthenticationFailureKind.AccountDeleted);
    }

    [Fact]
    public async Task LoginWithEmailAsync_WithValidPassword_ResetsFailedAttempts()
    {
        var user = UserAccount.CreateEmailUser(
            "user-1",
            "reader@example.com",
            "Reader",
            "hashed",
            ["query:read"],
            DateTimeOffset.UtcNow) with { FailedLoginAttempts = 2 };

        _userRepository.FindByEmailAsync("reader@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.VerifyPassword("correct-horse-battery-staple", "hashed").Returns(true);
        _tokenIssuer.IssueAccessToken(Arg.Any<UserAccount>(), Arg.Any<CancellationToken>())
            .Returns(new AuthTokenResult("token", 3600));

        var service = CreateService();
        await service.LoginWithEmailAsync("reader@example.com", "correct-horse-battery-staple", CancellationToken.None);

        await _userRepository.Received(1).UpdateAsync(
            Arg.Is<UserAccount>(account => account.FailedLoginAttempts == 0 && account.LockedUntilUtc == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExchangeCodeAsync_WithValidCode_IssuesToken()
    {
        var user = UserAccount.CreateEmailUser(
            "user-1",
            "reader@example.com",
            "Reader",
            "hashed",
            ["query:read"],
            DateTimeOffset.UtcNow);

        _exchangeCodeStore.ConsumeExchangeCodeAsync("exchange-code", Arg.Any<CancellationToken>()).Returns("user-1");
        _userRepository.FindByIdAsync("user-1", Arg.Any<CancellationToken>()).Returns(user);
        _tokenIssuer.IssueAccessToken(user, Arg.Any<CancellationToken>()).Returns(new AuthTokenResult("token", 3600));

        var service = CreateService();
        var result = await service.ExchangeCodeAsync("exchange-code", CancellationToken.None);

        result.AccessToken.Should().Be("token");
    }

    [Fact]
    public async Task BeginOAuthLoginAsync_StoresStateAndReturnsProviderUrl()
    {
        var service = CreateService();

        var redirectUrl = await service.BeginOAuthLoginAsync(
            AuthProviderKind.Google,
            "/query",
            "http://localhost:5281",
            CancellationToken.None);

        redirectUrl.Should().StartWith("https://accounts.google.com/o/oauth2/v2/auth");
        await _oauthStateStore.Received(1).StoreAuthorizationRequestAsync(
            Arg.Is<OAuthAuthorizationRequest>(request => request.Provider == AuthProviderKind.Google && request.ReturnTo == "/query"),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetProfileAsync_ReturnsMappedProfile()
    {
        var user = UserAccount.CreateEmailUser(
            "user-1",
            "reader@example.com",
            "Reader",
            "hashed",
            ["query:read"],
            DateTimeOffset.UtcNow);

        _userRepository.FindByIdAsync("user-1", Arg.Any<CancellationToken>()).Returns(user);

        var service = CreateService();
        var profile = await service.GetProfileAsync("user-1", CancellationToken.None);

        profile.UserId.Should().Be("user-1");
        profile.Email.Should().Be("reader@example.com");
        profile.HasPasswordLogin.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateProfileAsync_WithDuplicateEmail_ThrowsAuthenticationException()
    {
        var user = UserAccount.CreateEmailUser(
            "user-1",
            "reader@example.com",
            "Reader",
            "hashed",
            ["query:read"],
            DateTimeOffset.UtcNow);
        var other = UserAccount.CreateEmailUser(
            "user-2",
            "other@example.com",
            "Other",
            "hashed",
            ["query:read"],
            DateTimeOffset.UtcNow);

        _userRepository.FindByIdAsync("user-1", Arg.Any<CancellationToken>()).Returns(user);
        _userRepository.FindByEmailAsync("other@example.com", Arg.Any<CancellationToken>()).Returns(other);

        var service = CreateService();

        await service
            .Invoking(s => s.UpdateProfileAsync("user-1", null, "other@example.com", CancellationToken.None))
            .Should()
            .ThrowAsync<AuthenticationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task UpdateProfileAsync_WithEmailChange_IssuesNewToken()
    {
        var user = UserAccount.CreateEmailUser(
            "user-1",
            "reader@example.com",
            "Reader",
            "hashed",
            ["query:read"],
            DateTimeOffset.UtcNow);

        _userRepository.FindByIdAsync("user-1", Arg.Any<CancellationToken>()).Returns(user);
        _userRepository.FindByEmailAsync("updated@example.com", Arg.Any<CancellationToken>()).Returns((UserAccount?)null);
        _tokenIssuer.IssueAccessToken(Arg.Any<UserAccount>(), Arg.Any<CancellationToken>())
            .Returns(new AuthTokenResult("new-token", 3600));

        var service = CreateService();
        var result = await service.UpdateProfileAsync("user-1", null, "updated@example.com", CancellationToken.None);

        result.Profile.Email.Should().Be("updated@example.com");
        result.AccessToken!.AccessToken.Should().Be("new-token");
        await _userRepository.Received(1).UpdateAsync(
            Arg.Is<UserAccount>(account => account.Email == "updated@example.com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangePasswordAsync_WithInvalidCurrentPassword_ThrowsAuthenticationException()
    {
        var user = UserAccount.CreateEmailUser(
            "user-1",
            "reader@example.com",
            "Reader",
            "hashed",
            ["query:read"],
            DateTimeOffset.UtcNow);

        _userRepository.FindByIdAsync("user-1", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.VerifyPassword("wrong-password", "hashed").Returns(false);

        var service = CreateService();

        await service
            .Invoking(s => s.ChangePasswordAsync("user-1", "wrong-password", "new-password-long", CancellationToken.None))
            .Should()
            .ThrowAsync<AuthenticationException>();
    }

    [Fact]
    public async Task ChangePasswordAsync_WithValidPassword_IssuesNewToken()
    {
        var user = UserAccount.CreateEmailUser(
            "user-1",
            "reader@example.com",
            "Reader",
            "hashed",
            ["query:read"],
            DateTimeOffset.UtcNow);

        _userRepository.FindByIdAsync("user-1", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.VerifyPassword("current-password-long", "hashed").Returns(true);
        _passwordHasher.VerifyPassword("new-password-longer", "hashed").Returns(false);
        _passwordHasher.HashPassword("new-password-longer").Returns("new-hash");
        _tokenIssuer.IssueAccessToken(Arg.Any<UserAccount>(), Arg.Any<CancellationToken>())
            .Returns(new AuthTokenResult("token", 3600));

        var service = CreateService();
        var result = await service.ChangePasswordAsync(
            "user-1",
            "current-password-long",
            "new-password-longer",
            CancellationToken.None);

        result.AccessToken.Should().Be("token");
        await _userRepository.Received(1).UpdateAsync(
            Arg.Is<UserAccount>(account => account.PasswordHash == "new-hash" && account.FailedLoginAttempts == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAccountAsync_SoftDeletesUser()
    {
        var user = UserAccount.CreateEmailUser(
            "user-1",
            "reader@example.com",
            "Reader",
            "hashed",
            ["query:read"],
            DateTimeOffset.UtcNow);

        _userRepository.FindByIdAsync("user-1", Arg.Any<CancellationToken>()).Returns(user);
        _userRepository.SoftDeleteAsync("user-1", Arg.Any<CancellationToken>()).Returns(true);

        var service = CreateService();
        await service.DeleteAccountAsync("user-1", CancellationToken.None);

        await _userRepository.Received(1).SoftDeleteAsync("user-1", Arg.Any<CancellationToken>());
    }

    private AuthenticationService CreateService()
    {
        return new AuthenticationService(
            _userRepository,
            _passwordHasher,
            _tokenIssuer,
            _oauthStateStore,
            _exchangeCodeStore,
            _oauthProviderRegistry,
            Options.Create(new EmailAuthOptions
            {
                Enabled = true,
                MinPasswordLength = 12,
                DefaultScopes = ["query:read"],
                LockoutThreshold = 5,
                LockoutDurationMinutes = 15
            }),
            Options.Create(new GoogleOAuthOptions { Enabled = true, ClientId = "google-client", ClientSecret = "secret" }),
            Options.Create(new GitHubOAuthOptions { Enabled = true, ClientId = "github-client", ClientSecret = "secret" }),
            Options.Create(new AuthFrontendOptions { CallbackUrl = "http://localhost:5173/auth/callback" }),
            NullLogger<AuthenticationService>.Instance);
    }
}
