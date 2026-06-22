namespace DistributedQuery.Core.Models;

public sealed record OAuthUserProfile(
    string ProviderKey,
    string Email,
    string DisplayName);
