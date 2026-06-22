namespace DistributedQuery.Infrastructure.Auth;

public sealed class JwtSigningOptions
{
    public const string SectionName = "Authentication:JwtSigning";

    public string Issuer { get; init; } = "https://localhost:5281/";

    public string Audience { get; init; } = "dqee-api";

    public int AccessTokenLifetimeSeconds { get; init; } = 3600;

    public string? PrivateKeyPem { get; init; }

    public string? PublicKeyPem { get; init; }
}
