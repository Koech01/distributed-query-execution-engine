using System.Diagnostics;
using System.Security.Cryptography;
using DistributedQuery.Core.Exceptions;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using DistributedQuery.Infrastructure.Auth;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DistributedQuery.Api.Services;

public sealed class AuthenticationService
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Api.AuthenticationService");

    private static readonly TimeSpan OAuthStateTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ExchangeCodeTtl = TimeSpan.FromMinutes(2);

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuthTokenIssuer _tokenIssuer;
    private readonly IOAuthStateStore _oauthStateStore;
    private readonly IAuthExchangeCodeStore _exchangeCodeStore;
    private readonly OAuthProviderRegistry _oauthProviderRegistry;
    private readonly EmailAuthOptions _emailOptions;
    private readonly GoogleOAuthOptions _googleOptions;
    private readonly GitHubOAuthOptions _githubOptions;
    private readonly AuthFrontendOptions _frontendOptions;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IAuthTokenIssuer tokenIssuer,
        IOAuthStateStore oauthStateStore,
        IAuthExchangeCodeStore exchangeCodeStore,
        OAuthProviderRegistry oauthProviderRegistry,
        IOptions<EmailAuthOptions> emailOptions,
        IOptions<GoogleOAuthOptions> googleOptions,
        IOptions<GitHubOAuthOptions> githubOptions,
        IOptions<AuthFrontendOptions> frontendOptions,
        ILogger<AuthenticationService> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenIssuer = tokenIssuer;
        _oauthStateStore = oauthStateStore;
        _exchangeCodeStore = exchangeCodeStore;
        _oauthProviderRegistry = oauthProviderRegistry;
        _emailOptions = emailOptions.Value;
        _googleOptions = googleOptions.Value;
        _githubOptions = githubOptions.Value;
        _frontendOptions = frontendOptions.Value;
        _logger = logger;
    }

    public async Task<AuthTokenResult> RegisterWithEmailAsync(
        string email,
        string password,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("AuthenticationService.RegisterWithEmail", ActivityKind.Internal);

        EnsureEmailAuthEnabled();
        ValidateEmail(email);
        ValidatePassword(password);
        ValidateDisplayName(displayName);

        var existing = await _userRepository.FindByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            throw AuthenticationException.EmailAlreadyExists();
        }

        var timestamp = DateTimeOffset.UtcNow;
        var user = UserAccount.CreateEmailUser(
            Guid.NewGuid().ToString("N"),
            NormalizeEmail(email),
            displayName.Trim(),
            _passwordHasher.HashPassword(password),
            _emailOptions.DefaultScopes,
            timestamp);

        await _userRepository.CreateAsync(user, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Registered email user {UserId}", user.UserId);

        return _tokenIssuer.IssueAccessToken(user, cancellationToken);
    }

    public async Task<AuthTokenResult> LoginWithEmailAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("AuthenticationService.LoginWithEmail", ActivityKind.Internal);

        EnsureEmailAuthEnabled();
        ValidateEmail(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var user = await _userRepository.FindByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            _logger.LogWarning("Email login failed for unknown email.");
            throw AuthenticationException.InvalidCredentials();
        }

        EnsureAccountCanAuthenticate(user);

        var timestamp = DateTimeOffset.UtcNow;
        if (user.LockedUntilUtc.HasValue && timestamp >= user.LockedUntilUtc.Value)
        {
            user = user.WithResetFailedAttempts(timestamp);
            await _userRepository.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash) ||
            !_passwordHasher.VerifyPassword(password, user.PasswordHash))
        {
            var lockoutDuration = TimeSpan.FromMinutes(_emailOptions.LockoutDurationMinutes);
            var updated = user.WithFailedLoginAttempt(_emailOptions.LockoutThreshold, lockoutDuration, timestamp);
            await _userRepository.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);

            _logger.LogWarning(
                "Email login failed for user {UserId}. FailedAttempts={FailedAttempts}, Locked={Locked}",
                user.UserId,
                updated.FailedLoginAttempts,
                updated.IsLockedOut);

            if (updated.IsLockedOut && updated.LockedUntilUtc.HasValue)
            {
                throw AuthenticationException.AccountLocked(updated.LockedUntilUtc.Value);
            }

            throw AuthenticationException.InvalidCredentials();
        }

        if (user.FailedLoginAttempts > 0 || user.LockedUntilUtc.HasValue)
        {
            user = user.WithResetFailedAttempts(timestamp);
            await _userRepository.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation("Email login succeeded for user {UserId}", user.UserId);
        return _tokenIssuer.IssueAccessToken(user, cancellationToken);
    }

    public async Task<string> BeginOAuthLoginAsync(
        AuthProviderKind provider,
        string returnTo,
        string callbackBaseUrl,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("AuthenticationService.BeginOAuthLogin", ActivityKind.Internal);
        activity?.SetTag("auth.provider", provider.ToString());

        EnsureOAuthProviderEnabled(provider);

        var state = CreateRandomToken();
        var codeVerifier = provider == AuthProviderKind.Google ? CreateRandomToken() : null;
        var authorizationRequest = new OAuthAuthorizationRequest(
            provider,
            SanitizeReturnTo(returnTo),
            state,
            codeVerifier);

        await _oauthStateStore
            .StoreAuthorizationRequestAsync(authorizationRequest, OAuthStateTtl, cancellationToken)
            .ConfigureAwait(false);

        var oauthClient = _oauthProviderRegistry.GetRequiredProvider(provider);
        var redirectUri = BuildProviderCallbackUrl(callbackBaseUrl, provider);
        return oauthClient.BuildAuthorizationUrl(authorizationRequest, redirectUri);
    }

    public async Task<string> CompleteOAuthLoginAsync(
        AuthProviderKind provider,
        string code,
        string state,
        string callbackBaseUrl,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("AuthenticationService.CompleteOAuthLogin", ActivityKind.Internal);
        activity?.SetTag("auth.provider", provider.ToString());

        EnsureOAuthProviderEnabled(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(state);

        var authorizationRequest = await _oauthStateStore
            .ConsumeAuthorizationRequestAsync(provider, state, cancellationToken)
            .ConfigureAwait(false);

        if (authorizationRequest is null)
        {
            throw new AuthenticationException("OAuth state is invalid or expired.");
        }

        var oauthClient = _oauthProviderRegistry.GetRequiredProvider(provider);
        var redirectUri = BuildProviderCallbackUrl(callbackBaseUrl, provider);
        var profile = await oauthClient
            .ExchangeAuthorizationCodeAsync(code, redirectUri, authorizationRequest.CodeVerifier, cancellationToken)
            .ConfigureAwait(false);

        var user = await ResolveOAuthUserAsync(provider, profile, cancellationToken).ConfigureAwait(false);
        var exchangeCode = await _exchangeCodeStore
            .CreateExchangeCodeAsync(user.UserId, ExchangeCodeTtl, cancellationToken)
            .ConfigureAwait(false);

        return BuildFrontendCallbackUrl(exchangeCode, authorizationRequest.ReturnTo);
    }

    public async Task<AuthTokenResult> ExchangeCodeAsync(string exchangeCode, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("AuthenticationService.ExchangeCode", ActivityKind.Internal);

        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeCode);

        var userId = await _exchangeCodeStore.ConsumeExchangeCodeAsync(exchangeCode, cancellationToken).ConfigureAwait(false);
        if (userId is null)
        {
            throw new AuthenticationException("Exchange code is invalid or expired.");
        }

        var user = await _userRepository.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            throw new AuthenticationException("Authenticated user account was not found.");
        }

        EnsureAccountCanAuthenticate(user);

        return _tokenIssuer.IssueAccessToken(user, cancellationToken);
    }

    public async Task<UserProfile> GetProfileAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("AuthenticationService.GetProfile", ActivityKind.Internal);
        activity?.SetTag("auth.user_id", userId);

        var user = await RequireActiveUserAsync(userId, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Loaded profile for user {UserId}", user.UserId);
        return UserProfile.FromAccount(user);
    }

    public async Task<ProfileUpdateResult> UpdateProfileAsync(
        string userId,
        string? displayName,
        string? email,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("AuthenticationService.UpdateProfile", ActivityKind.Internal);
        activity?.SetTag("auth.user_id", userId);

        if (displayName is null && email is null)
        {
            throw new AuthenticationException("At least one profile field must be provided.");
        }

        var user = await RequireActiveUserAsync(userId, cancellationToken).ConfigureAwait(false);
        var timestamp = DateTimeOffset.UtcNow;
        var updated = user;
        var emailChanged = false;
        var profileChanged = false;

        if (displayName is not null)
        {
            ValidateDisplayName(displayName);
            var trimmedDisplayName = displayName.Trim();
            if (!string.Equals(trimmedDisplayName, user.DisplayName, StringComparison.Ordinal))
            {
                updated = updated.WithDisplayName(trimmedDisplayName, timestamp);
                profileChanged = true;
            }
        }

        if (email is not null)
        {
            ValidateEmail(email);
            var normalizedEmail = NormalizeEmail(email);
            if (!string.Equals(normalizedEmail, user.Email, StringComparison.Ordinal))
            {
                var existing = await _userRepository.FindByEmailAsync(normalizedEmail, cancellationToken).ConfigureAwait(false);
                if (existing is not null && !string.Equals(existing.UserId, user.UserId, StringComparison.Ordinal))
                {
                    throw AuthenticationException.EmailAlreadyExists();
                }

                updated = updated.WithEmail(normalizedEmail, timestamp);
                emailChanged = true;
                profileChanged = true;
            }
        }

        if (!profileChanged)
        {
            return new ProfileUpdateResult(UserProfile.FromAccount(user), null);
        }

        await _userRepository.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Updated profile for user {UserId}. EmailChanged={EmailChanged}", user.UserId, emailChanged);

        var token = emailChanged
            ? _tokenIssuer.IssueAccessToken(updated, cancellationToken)
            : null;

        return new ProfileUpdateResult(UserProfile.FromAccount(updated), token);
    }

    public async Task<AuthTokenResult> ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("AuthenticationService.ChangePassword", ActivityKind.Internal);
        activity?.SetTag("auth.user_id", userId);

        EnsureEmailAuthEnabled();
        ArgumentException.ThrowIfNullOrWhiteSpace(currentPassword);
        ValidatePassword(newPassword);

        var user = await RequireActiveUserAsync(userId, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            throw new AuthenticationException("Password login is not available for this account.");
        }

        if (!_passwordHasher.VerifyPassword(currentPassword, user.PasswordHash))
        {
            _logger.LogWarning("Password change rejected for user {UserId} due to invalid current password.", user.UserId);
            throw AuthenticationException.InvalidCredentials();
        }

        if (_passwordHasher.VerifyPassword(newPassword, user.PasswordHash))
        {
            throw new AuthenticationException("New password must be different from the current password.");
        }

        var timestamp = DateTimeOffset.UtcNow;
        var updated = user
            .WithPasswordHash(_passwordHasher.HashPassword(newPassword), timestamp)
            .WithResetFailedAttempts(timestamp);

        await _userRepository.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Changed password for user {UserId}", user.UserId);

        return _tokenIssuer.IssueAccessToken(updated, cancellationToken);
    }

    public async Task DeleteAccountAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("AuthenticationService.DeleteAccount", ActivityKind.Internal);
        activity?.SetTag("auth.user_id", userId);

        var user = await RequireActiveUserAsync(userId, cancellationToken).ConfigureAwait(false);
        var deleted = await _userRepository.SoftDeleteAsync(user.UserId, cancellationToken).ConfigureAwait(false);
        if (!deleted)
        {
            throw new AuthenticationException("Account could not be deleted.");
        }

        _logger.LogInformation("Soft-deleted account for user {UserId}", user.UserId);
    }

    private async Task<UserAccount> RequireActiveUserAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var user = await _userRepository.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            throw new AuthenticationException("Authenticated user account was not found.");
        }

        EnsureAccountCanAuthenticate(user);
        return user;
    }

    private async Task<UserAccount> ResolveOAuthUserAsync(
        AuthProviderKind provider,
        OAuthUserProfile profile,
        CancellationToken cancellationToken)
    {
        var existingByProvider = await _userRepository
            .FindByExternalLoginAsync(provider, profile.ProviderKey, cancellationToken)
            .ConfigureAwait(false);

        if (existingByProvider is not null)
        {
            EnsureAccountCanAuthenticate(existingByProvider);
            return existingByProvider;
        }

        var normalizedEmail = NormalizeEmail(profile.Email);
        var existingByEmail = await _userRepository.FindByEmailAsync(normalizedEmail, cancellationToken).ConfigureAwait(false);
        if (existingByEmail is not null)
        {
            EnsureAccountCanAuthenticate(existingByEmail);
            var linked = existingByEmail.WithExternalLogin(new ExternalLogin(provider.ToString(), profile.ProviderKey), DateTimeOffset.UtcNow);
            await _userRepository.UpdateAsync(linked, cancellationToken).ConfigureAwait(false);
            return linked;
        }

        var timestamp = DateTimeOffset.UtcNow;
        var created = UserAccount.CreateOAuthUser(
            Guid.NewGuid().ToString("N"),
            normalizedEmail,
            profile.DisplayName,
            provider,
            profile.ProviderKey,
            _emailOptions.DefaultScopes,
            timestamp);

        await _userRepository.CreateAsync(created, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Created OAuth user {UserId} via provider {Provider}", created.UserId, provider);
        return created;
    }

    private string BuildProviderCallbackUrl(string callbackBaseUrl, AuthProviderKind provider)
    {
        var callbackPath = provider switch
        {
            AuthProviderKind.Google => _googleOptions.CallbackPath,
            AuthProviderKind.GitHub => _githubOptions.CallbackPath,
            _ => throw new InvalidOperationException($"Unsupported OAuth provider '{provider}'."),
        };

        return new Uri(new Uri(callbackBaseUrl.TrimEnd('/') + "/"), callbackPath.TrimStart('/')).ToString();
    }

    private string BuildFrontendCallbackUrl(string exchangeCode, string returnTo)
    {
        return QueryHelpers.AddQueryString(_frontendOptions.CallbackUrl, new Dictionary<string, string?>
        {
            ["exchangeCode"] = exchangeCode,
            ["returnTo"] = SanitizeReturnTo(returnTo),
        });
    }

    private void EnsureAccountCanAuthenticate(UserAccount user)
    {
        if (user.IsDeleted)
        {
            _logger.LogWarning("Authentication attempt on deleted account {UserId}", user.UserId);
            throw AuthenticationException.AccountDeleted();
        }

        if (user.IsLockedOut && user.LockedUntilUtc.HasValue)
        {
            _logger.LogWarning(
                "Authentication attempt on locked account {UserId}. LockedUntil={LockedUntil}",
                user.UserId,
                user.LockedUntilUtc);
            throw AuthenticationException.AccountLocked(user.LockedUntilUtc.Value);
        }
    }

    private void EnsureEmailAuthEnabled()
    {
        if (!_emailOptions.Enabled)
        {
            throw new AuthenticationException("Email and password authentication is disabled.");
        }
    }

    private void EnsureOAuthProviderEnabled(AuthProviderKind provider)
    {
        if (!_oauthProviderRegistry.IsEnabled(provider, _googleOptions, _githubOptions))
        {
            throw new AuthenticationException($"{provider} authentication is disabled.");
        }
    }

    private void ValidateEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        if (!email.Contains('@', StringComparison.Ordinal))
        {
            throw new AuthenticationException("Email address is invalid.");
        }
    }

    private void ValidateDisplayName(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (displayName.Trim().Length is < 2 or > 100)
        {
            throw new AuthenticationException("Display name must be between 2 and 100 characters.");
        }
    }

    private void ValidatePassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        if (password.Length < _emailOptions.MinPasswordLength)
        {
            throw new AuthenticationException($"Password must be at least {_emailOptions.MinPasswordLength} characters.");
        }
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string SanitizeReturnTo(string returnTo)
    {
        if (string.IsNullOrWhiteSpace(returnTo) || !returnTo.StartsWith('/') || returnTo.StartsWith("//"))
        {
            return "/query";
        }

        return returnTo;
    }

    private static string CreateRandomToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
}
