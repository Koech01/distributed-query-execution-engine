using DistributedQuery.Core.Models;

namespace DistributedQuery.Core.Interfaces;

public interface IOAuthProviderClient
{
    AuthProviderKind Provider { get; }

    string BuildAuthorizationUrl(OAuthAuthorizationRequest request, string redirectUri);

    Task<OAuthUserProfile> ExchangeAuthorizationCodeAsync(
        string code,
        string redirectUri,
        string? codeVerifier,
        CancellationToken cancellationToken = default);
}
