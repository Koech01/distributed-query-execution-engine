using DistributedQuery.Core.Models;

namespace DistributedQuery.Core.Interfaces;

public interface IUserRepository
{
    Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<UserAccount?> FindByExternalLoginAsync(
        AuthProviderKind provider,
        string providerKey,
        CancellationToken cancellationToken = default);

    Task<UserAccount?> FindByIdAsync(string userId, CancellationToken cancellationToken = default);

    Task CreateAsync(UserAccount user, CancellationToken cancellationToken = default);

    Task UpdateAsync(UserAccount user, CancellationToken cancellationToken = default);

    Task<bool> SoftDeleteAsync(string userId, CancellationToken cancellationToken = default);
}
