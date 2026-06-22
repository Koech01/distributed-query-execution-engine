namespace DistributedQuery.Infrastructure.Auth;

public sealed class AuthSeedOptions
{
    public const string SectionName = "Authentication:Seed";

    public bool Enabled { get; init; } = true;

    public SeedAccountOptions Admin { get; init; } = new();

    public SeedAccountOptions User { get; init; } = new();
}

public sealed class SeedAccountOptions
{
    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public IReadOnlyList<string> Scopes { get; init; } = ["query:read"];
}
