namespace DistributedQuery.Infrastructure.Auth;

public sealed class GitHubOAuthOptions
{
    public const string SectionName = "Authentication:GitHub";

    public bool Enabled { get; init; }

    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public string CallbackPath { get; init; } = "/auth/github/callback";
}
