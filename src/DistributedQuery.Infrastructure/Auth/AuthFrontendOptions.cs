namespace DistributedQuery.Infrastructure.Auth;

public sealed class AuthFrontendOptions
{
    public const string SectionName = "Authentication:Frontend";

    public string CallbackUrl { get; init; } = "http://localhost:5173/auth/callback";
}
