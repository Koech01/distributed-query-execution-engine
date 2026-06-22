namespace DistributedQuery.Infrastructure.Auth;

public sealed class GoogleOAuthOptions
{
    public const string SectionName = "Authentication:Google";

    public bool Enabled { get; init; }

    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public string CallbackPath { get; init; } = "/auth/google/callback";
}
