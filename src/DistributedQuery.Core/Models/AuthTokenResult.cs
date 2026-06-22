namespace DistributedQuery.Core.Models;

public sealed record AuthTokenResult(
    string AccessToken,
    int ExpiresInSeconds,
    string TokenType = "Bearer");
