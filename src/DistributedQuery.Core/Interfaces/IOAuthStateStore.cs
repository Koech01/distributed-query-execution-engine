using DistributedQuery.Core.Models;

namespace DistributedQuery.Core.Interfaces;

public interface IOAuthStateStore
{
    Task StoreAuthorizationRequestAsync(
        OAuthAuthorizationRequest request,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);

    Task<OAuthAuthorizationRequest?> ConsumeAuthorizationRequestAsync(
        AuthProviderKind provider,
        string state,
        CancellationToken cancellationToken = default);
}
