namespace DistributedQuery.Infrastructure.Auth;

public sealed class EmailAuthOptions
{
    public const string SectionName = "Authentication:Email";

    public bool Enabled { get; init; } = true;

    public int MinPasswordLength { get; init; } = 12;

    public IReadOnlyList<string> DefaultScopes { get; init; } = ["query:read"];

    public int LockoutThreshold { get; init; } = 5;

    public int LockoutDurationMinutes { get; init; } = 15;
}
