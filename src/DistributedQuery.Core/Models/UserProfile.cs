namespace DistributedQuery.Core.Models;

public sealed record UserProfile(
    string UserId,
    string Email,
    string? DisplayName,
    bool HasPasswordLogin,
    IReadOnlyList<string> LinkedProviders,
    IReadOnlyList<string> Scopes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static UserProfile FromAccount(UserAccount account) =>
        new(
            account.UserId,
            account.Email,
            account.DisplayName,
            !string.IsNullOrWhiteSpace(account.PasswordHash),
            account.ExternalLogins.Select(static login => login.Provider).ToArray(),
            account.Scopes,
            account.CreatedAt,
            account.UpdatedAt);
}

public sealed record ProfileUpdateResult(UserProfile Profile, AuthTokenResult? AccessToken);
