using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DistributedQuery.Core.Exceptions;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DistributedQuery.Infrastructure.Auth;

public sealed class GitHubOAuthClient : IOAuthProviderClient
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Auth.GitHubOAuth");

    private readonly HttpClient _httpClient;
    private readonly GitHubOAuthOptions _options;
    private readonly ILogger<GitHubOAuthClient> _logger;

    public GitHubOAuthClient(
        HttpClient httpClient,
        IOptions<GitHubOAuthOptions> options,
        ILogger<GitHubOAuthClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public AuthProviderKind Provider => AuthProviderKind.GitHub;

    public string BuildAuthorizationUrl(OAuthAuthorizationRequest request, string redirectUri)
    {
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = "read:user user:email",
            ["state"] = request.State,
        };

        return QueryHelpers.AddQueryString("https://github.com/login/oauth/authorize", query!);
    }

    public async Task<OAuthUserProfile> ExchangeAuthorizationCodeAsync(
        string code,
        string redirectUri,
        string? codeVerifier,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("GitHubOAuthClient.ExchangeAuthorizationCode", ActivityKind.Client);

        var accessToken = await RequestAccessTokenAsync(code, redirectUri, cancellationToken).ConfigureAwait(false);

        using var userRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
        userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        userRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        userRequest.Headers.UserAgent.ParseAdd("DistributedQueryExecutionEngine/1.0");

        using var userResponse = await _httpClient.SendAsync(userRequest, cancellationToken).ConfigureAwait(false);
        var profile = await userResponse.Content.ReadFromJsonAsync<GitHubUserInfo>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!userResponse.IsSuccessStatusCode || profile is null || profile.Id <= 0)
        {
            throw new AuthenticationException("GitHub user profile request failed.");
        }

        var email = profile.Email;
        if (string.IsNullOrWhiteSpace(email))
        {
            email = await ResolvePrimaryEmailAsync(accessToken, cancellationToken).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new AuthenticationException("GitHub user profile did not include a verified email address.");
        }

        _logger.LogInformation("Resolved GitHub OAuth profile for subject {Subject}", profile.Id);

        return new OAuthUserProfile(
            profile.Id.ToString(),
            email,
            profile.Name ?? profile.Login ?? email);
    }

    private async Task<string?> ResolvePrimaryEmailAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var emailRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/emails");
        emailRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        emailRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        emailRequest.Headers.UserAgent.ParseAdd("DistributedQueryExecutionEngine/1.0");

        using var emailResponse = await _httpClient.SendAsync(emailRequest, cancellationToken).ConfigureAwait(false);
        if (!emailResponse.IsSuccessStatusCode)
        {
            return null;
        }

        var emails = await emailResponse.Content.ReadFromJsonAsync<List<GitHubEmailInfo>>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return emails?
            .FirstOrDefault(entry => entry.Primary && entry.Verified)?.Email
            ?? emails?.FirstOrDefault(entry => entry.Verified)?.Email;
    }

    private async Task<string> RequestAccessTokenAsync(
        string code,
        string redirectUri,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
            }),
            Headers =
            {
                Accept = { new MediaTypeWithQualityHeaderValue("application/json") },
            },
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var payload = await response.Content.ReadFromJsonAsync<GitHubTokenResponse>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode || payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            throw new AuthenticationException("GitHub authorization code exchange failed.");
        }

        return payload.AccessToken;
    }

    private sealed class GitHubTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }
    }

    private sealed class GitHubUserInfo
    {
        [JsonPropertyName("id")]
        public long Id { get; init; }

        [JsonPropertyName("login")]
        public string? Login { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("email")]
        public string? Email { get; init; }
    }

    private sealed class GitHubEmailInfo
    {
        [JsonPropertyName("email")]
        public string? Email { get; init; }

        [JsonPropertyName("primary")]
        public bool Primary { get; init; }

        [JsonPropertyName("verified")]
        public bool Verified { get; init; }
    }
}
