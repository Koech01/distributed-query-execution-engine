using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;

namespace DistributedQuery.Infrastructure.Auth;

public sealed class OAuthProviderRegistry
{
    private readonly IReadOnlyDictionary<AuthProviderKind, IOAuthProviderClient> _providers;

    public OAuthProviderRegistry(GoogleOAuthClient googleOAuthClient, GitHubOAuthClient gitHubOAuthClient)
    {
        _providers = new Dictionary<AuthProviderKind, IOAuthProviderClient>
        {
            [AuthProviderKind.Google] = googleOAuthClient,
            [AuthProviderKind.GitHub] = gitHubOAuthClient,
        };
    }

    public IOAuthProviderClient GetRequiredProvider(AuthProviderKind provider)
    {
        if (!_providers.TryGetValue(provider, out var client))
        {
            throw new InvalidOperationException($"OAuth provider '{provider}' is not registered.");
        }

        return client;
    }

    public bool IsEnabled(AuthProviderKind provider, GoogleOAuthOptions googleOptions, GitHubOAuthOptions githubOptions) =>
        provider switch
        {
            AuthProviderKind.Google => googleOptions.Enabled,
            AuthProviderKind.GitHub => githubOptions.Enabled,
            _ => false,
        };
}
