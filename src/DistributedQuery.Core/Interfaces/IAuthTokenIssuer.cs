using DistributedQuery.Core.Models;

namespace DistributedQuery.Core.Interfaces;

public interface IAuthTokenIssuer
{
    AuthTokenResult IssueAccessToken(UserAccount user, CancellationToken cancellationToken = default);
}
