using System.Diagnostics;
using DistributedQuery.Api.Contracts;
using DistributedQuery.Api.Services;
using DistributedQuery.Core.Models;
using DistributedQuery.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DistributedQuery.Api.Controllers;

[ApiController]
[Route("auth/account")]
[Authorize(Policy = ApiServiceCollectionExtensions.QueryReadPolicy)]
public sealed class AccountController : ControllerBase
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Api.AccountController");

    private readonly AuthenticationService _authenticationService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(AuthenticationService authenticationService, ILogger<AccountController> logger)
    {
        _authenticationService = authenticationService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileResponse>> GetProfileAsync(CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("api.auth.account.get", ActivityKind.Server);

        var userId = GetAuthenticatedUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        activity?.SetTag("auth.user_id", userId);
        var profile = await _authenticationService.GetProfileAsync(userId, cancellationToken).ConfigureAwait(false);
        return Ok(ToResponse(profile));
    }

    [HttpPatch]
    [ProducesResponseType(typeof(UpdateProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UpdateProfileResponse>> UpdateProfileAsync(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("api.auth.account.update", ActivityKind.Server);

        var userId = GetAuthenticatedUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        activity?.SetTag("auth.user_id", userId);
        var result = await _authenticationService
            .UpdateProfileAsync(userId, request.DisplayName, request.Email, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation("Profile updated for user {UserId}", userId);

        if (result.AccessToken is not null)
        {
            AuthAccessTokenCookie.Append(HttpContext, result.AccessToken);
        }

        return Ok(new UpdateProfileResponse
        {
            Profile = ToResponse(result.Profile),
            Token = result.AccessToken is null ? null : ToTokenResponse(result.AccessToken),
        });
    }

    [HttpPost("change-password")]
    [EnableRateLimiting(AuthRateLimitingExtensions.AuthIpPolicy)]
    [ProducesResponseType(typeof(AuthTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthTokenResponse>> ChangePasswordAsync(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("api.auth.account.change_password", ActivityKind.Server);

        var userId = GetAuthenticatedUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        activity?.SetTag("auth.user_id", userId);
        var token = await _authenticationService
            .ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation("Password changed for user {UserId}", userId);
        AuthAccessTokenCookie.Append(HttpContext, token);
        return Ok(ToTokenResponse(token));
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteAccountAsync(CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("api.auth.account.delete", ActivityKind.Server);

        var userId = GetAuthenticatedUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        activity?.SetTag("auth.user_id", userId);
        await _authenticationService.DeleteAccountAsync(userId, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Account deleted for user {UserId}", userId);
        AuthAccessTokenCookie.Delete(HttpContext);
        return NoContent();
    }

    private string? GetAuthenticatedUserId() => User.GetUserId();

    private static UserProfileResponse ToResponse(UserProfile profile) =>
        new()
        {
            UserId = profile.UserId,
            Email = profile.Email,
            DisplayName = profile.DisplayName,
            HasPasswordLogin = profile.HasPasswordLogin,
            LinkedProviders = profile.LinkedProviders,
            Scopes = profile.Scopes,
            CreatedAt = profile.CreatedAt,
            UpdatedAt = profile.UpdatedAt,
        };

    private static AuthTokenResponse ToTokenResponse(AuthTokenResult token) =>
        new()
        {
            AccessToken = token.AccessToken,
            ExpiresIn = token.ExpiresInSeconds,
            TokenType = token.TokenType,
        };
}
