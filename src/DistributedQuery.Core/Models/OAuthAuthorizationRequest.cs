namespace DistributedQuery.Core.Models;

public sealed record OAuthAuthorizationRequest(
    AuthProviderKind Provider,
    string ReturnTo,
    string State,
    string? CodeVerifier = null);
