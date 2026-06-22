using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using DistributedQuery.Core.Exceptions;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DistributedQuery.Infrastructure.Auth;

public sealed class GoogleOAuthClient : IOAuthProviderClient
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Auth.GoogleOAuth");

    private readonly HttpClient _httpClient;
    private readonly GoogleOAuthOptions _options;
    private readonly ILogger<GoogleOAuthClient> _logger;

    public GoogleOAuthClient(
        HttpClient httpClient,
        IOptions<GoogleOAuthOptions> options,
        ILogger<GoogleOAuthClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public AuthProviderKind Provider => AuthProviderKind.Google;

    public string BuildAuthorizationUrl(OAuthAuthorizationRequest request, string redirectUri)
    {
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid email profile",
            ["state"] = request.State,
            ["access_type"] = "online",
            ["prompt"] = "select_account",
        };

        if (!string.IsNullOrWhiteSpace(request.CodeVerifier))
        {
            query["code_challenge"] = CreateCodeChallenge(request.CodeVerifier);
            query["code_challenge_method"] = "S256";
        }

        return QueryHelpers.AddQueryString("https://accounts.google.com/o/oauth2/v2/auth", query!);
    }

    public async Task<OAuthUserProfile> ExchangeAuthorizationCodeAsync(
        string code,
        string redirectUri,
        string? codeVerifier,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("GoogleOAuthClient.ExchangeAuthorizationCode", ActivityKind.Client);

        var tokenResponse = await RequestTokenAsync(code, redirectUri, codeVerifier, cancellationToken)
            .ConfigureAwait(false);

        using var userInfoRequest = new HttpRequestMessage(HttpMethod.Get, "https://openidconnect.googleapis.com/v1/userinfo");
        userInfoRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);

        using var userInfoResponse = await _httpClient.SendAsync(userInfoRequest, cancellationToken).ConfigureAwait(false);
        if (!userInfoResponse.IsSuccessStatusCode)
        {
            throw new AuthenticationException("Google user profile request failed.");
        }

        var profile = await userInfoResponse.Content.ReadFromJsonAsync<GoogleUserInfo>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (profile is null || string.IsNullOrWhiteSpace(profile.Sub) || string.IsNullOrWhiteSpace(profile.Email))
        {
            throw new AuthenticationException("Google user profile did not include required identity claims.");
        }

        _logger.LogInformation("Resolved Google OAuth profile for subject {Subject}", profile.Sub);

        return new OAuthUserProfile(
            profile.Sub,
            profile.Email,
            profile.Name ?? profile.Email);
    }

    private async Task<GoogleTokenResponse> RequestTokenAsync(
        string code,
        string redirectUri,
        string? codeVerifier,
        CancellationToken cancellationToken)
    {
        var formValues = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
        };

        if (!string.IsNullOrWhiteSpace(codeVerifier))
        {
            formValues["code_verifier"] = codeVerifier;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
        {
            Content = new FormUrlEncodedContent(formValues),
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var payload = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode || payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            throw new AuthenticationException("Google authorization code exchange failed.");
        }

        return payload;
    }

    private static string CreateCodeChallenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlHelpers.Encode(hash);
    }

    private sealed class GoogleTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }
    }

    private sealed class GoogleUserInfo
    {
        [JsonPropertyName("sub")]
        public string? Sub { get; init; }

        [JsonPropertyName("email")]
        public string? Email { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }
}
