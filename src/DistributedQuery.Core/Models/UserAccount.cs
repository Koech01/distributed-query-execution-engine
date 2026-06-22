namespace DistributedQuery.Core.Models;

public sealed record UserAccount(
    string UserId,
    string Email,
    string? DisplayName,
    string? PasswordHash,
    IReadOnlyList<ExternalLogin> ExternalLogins,
    IReadOnlyList<string> Scopes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int FailedLoginAttempts = 0,
    DateTimeOffset? LockedUntilUtc = null,
    DateTimeOffset? DeletedAt = null)
{
    public bool IsDeleted => DeletedAt.HasValue;

    public bool IsLockedOut => LockedUntilUtc.HasValue && DateTimeOffset.UtcNow < LockedUntilUtc.Value;

    public static UserAccount CreateEmailUser(
        string userId,
        string email,
        string displayName,
        string passwordHash,
        IReadOnlyList<string> scopes,
        DateTimeOffset timestamp)
    {
        return new UserAccount(
            userId,
            email,
            displayName,
            passwordHash,
            [],
            scopes,
            timestamp,
            timestamp);
    }

    public static UserAccount CreateOAuthUser(
        string userId,
        string email,
        string displayName,
        AuthProviderKind provider,
        string providerKey,
        IReadOnlyList<string> scopes,
        DateTimeOffset timestamp)
    {
        return new UserAccount(
            userId,
            email,
            displayName,
            null,
            [new ExternalLogin(provider.ToString(), providerKey)],
            scopes,
            timestamp,
            timestamp);
    }

    public UserAccount WithExternalLogin(ExternalLogin login, DateTimeOffset timestamp)
    {
        if (ExternalLogins.Any(existing =>
                existing.Provider.Equals(login.Provider, StringComparison.OrdinalIgnoreCase) &&
                existing.ProviderKey == login.ProviderKey))
        {
            return this;
        }

        var logins = ExternalLogins.ToList();
        logins.Add(login);
        return this with { ExternalLogins = logins, UpdatedAt = timestamp };
    }

    public UserAccount WithFailedLoginAttempt(int maxAttempts, TimeSpan lockoutDuration, DateTimeOffset timestamp)
    {
        var newAttempts = FailedLoginAttempts + 1;
        var lockedUntil = newAttempts >= maxAttempts
            ? timestamp.Add(lockoutDuration)
            : (DateTimeOffset?)null;

        return this with
        {
            FailedLoginAttempts = newAttempts,
            LockedUntilUtc = lockedUntil,
            UpdatedAt = timestamp
        };
    }

    public UserAccount WithResetFailedAttempts(DateTimeOffset timestamp) =>
        this with
        {
            FailedLoginAttempts = 0,
            LockedUntilUtc = null,
            UpdatedAt = timestamp
        };

    public UserAccount SoftDelete(DateTimeOffset timestamp) =>
        this with
        {
            DeletedAt = timestamp,
            UpdatedAt = timestamp
        };

    public UserAccount WithDisplayName(string displayName, DateTimeOffset timestamp) =>
        this with
        {
            DisplayName = displayName,
            UpdatedAt = timestamp
        };

    public UserAccount WithEmail(string email, DateTimeOffset timestamp) =>
        this with
        {
            Email = email,
            UpdatedAt = timestamp
        };

    public UserAccount WithPasswordHash(string passwordHash, DateTimeOffset timestamp) =>
        this with
        {
            PasswordHash = passwordHash,
            UpdatedAt = timestamp
        };
}
