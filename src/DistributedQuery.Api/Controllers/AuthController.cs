using DistributedQuery.Api;
using DistributedQuery.Api.Contracts;
using DistributedQuery.Api.Services;
using DistributedQuery.Core.Models;
using DistributedQuery.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace DistributedQuery.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AuthenticationService _authenticationService;
    private readonly AuthFrontendOptions _frontendOptions;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        AuthenticationService authenticationService,
        IOptions<AuthFrontendOptions> frontendOptions,
        ILogger<AuthController> logger)
    {
        _authenticationService = authenticationService;
        _frontendOptions = frontendOptions.Value;
        _logger = logger;
    }

    [HttpPost("register")]
    [EnableRateLimiting(AuthRateLimitingExtensions.AuthIpPolicy)]
    [ProducesResponseType(typeof(AuthTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthTokenResponse>> RegisterAsync(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var token = await _authenticationService
            .RegisterWithEmailAsync(request.Email, request.Password, request.DisplayName, cancellationToken)
            .ConfigureAwait(false);

        AuthAccessTokenCookie.Append(HttpContext, token);
        return Ok(ToResponse(token));
    }

    [HttpPost("login")]
    [EnableRateLimiting(AuthRateLimitingExtensions.AuthIpPolicy)]
    [ProducesResponseType(typeof(AuthTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthTokenResponse>> LoginAsync(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var token = await _authenticationService
            .LoginWithEmailAsync(request.Email, request.Password, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation("Issued email login token for authenticated session.");
        AuthAccessTokenCookie.Append(HttpContext, token);
        return Ok(ToResponse(token));
    }

    [HttpGet("google/login")]
    public async Task<IActionResult> GoogleLoginAsync(
        [FromQuery] string? returnTo,
        CancellationToken cancellationToken)
    {
        var redirectUrl = await _authenticationService
            .BeginOAuthLoginAsync(AuthProviderKind.Google, returnTo ?? "/query", GetCallbackBaseUrl(), cancellationToken)
            .ConfigureAwait(false);

        return Redirect(redirectUrl);
    }

    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallbackAsync(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            return RedirectToFrontendError(error);
        }

        var redirectUrl = await _authenticationService
            .CompleteOAuthLoginAsync(AuthProviderKind.Google, code!, state!, GetCallbackBaseUrl(), cancellationToken)
            .ConfigureAwait(false);

        return Redirect(redirectUrl);
    }

    [HttpGet("github/login")]
    public async Task<IActionResult> GitHubLoginAsync(
        [FromQuery] string? returnTo,
        CancellationToken cancellationToken)
    {
        var redirectUrl = await _authenticationService
            .BeginOAuthLoginAsync(AuthProviderKind.GitHub, returnTo ?? "/query", GetCallbackBaseUrl(), cancellationToken)
            .ConfigureAwait(false);

        return Redirect(redirectUrl);
    }

    [HttpGet("github/callback")]
    public async Task<IActionResult> GitHubCallbackAsync(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            return RedirectToFrontendError(error);
        }

        var redirectUrl = await _authenticationService
            .CompleteOAuthLoginAsync(AuthProviderKind.GitHub, code!, state!, GetCallbackBaseUrl(), cancellationToken)
            .ConfigureAwait(false);

        return Redirect(redirectUrl);
    }

    [HttpPost("token/exchange")]
    [ProducesResponseType(typeof(AuthTokenResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthTokenResponse>> ExchangeTokenAsync(
        [FromBody] ExchangeTokenRequest request,
        CancellationToken cancellationToken)
    {
        var token = await _authenticationService
            .ExchangeCodeAsync(request.ExchangeCode, cancellationToken)
            .ConfigureAwait(false);

        AuthAccessTokenCookie.Append(HttpContext, token);
        return Ok(ToResponse(token));
    }

    private string GetCallbackBaseUrl()
    {
        var request = HttpContext.Request;
        return $"{request.Scheme}://{request.Host}";
    }

    private IActionResult RedirectToFrontendError(string error)
    {
        var redirectUrl = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(
            _frontendOptions.CallbackUrl,
            new Dictionary<string, string?> { ["error"] = error });

        return Redirect(redirectUrl);
    }

    private static AuthTokenResponse ToResponse(AuthTokenResult token) =>
        new()
        {
            AccessToken = token.AccessToken,
            ExpiresIn = token.ExpiresInSeconds,
            TokenType = token.TokenType,
        };
}
