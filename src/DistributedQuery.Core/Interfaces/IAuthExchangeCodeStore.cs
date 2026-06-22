namespace DistributedQuery.Core.Interfaces;

public interface IAuthExchangeCodeStore
{
    Task<string> CreateExchangeCodeAsync(string userId, TimeSpan ttl, CancellationToken cancellationToken = default);

    Task<string?> ConsumeExchangeCodeAsync(string exchangeCode, CancellationToken cancellationToken = default);
}
