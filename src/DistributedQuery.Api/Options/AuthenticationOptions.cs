using System.ComponentModel.DataAnnotations;

namespace DistributedQuery.Api.Options;

public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";

    public bool Enabled { get; init; } = true;

    [Required]
    public string Authority { get; init; } = "https://auth.internal/";

    [Required]
    public string Audience { get; init; } = "dqee-api";

    public bool UseLocalJwtIssuer { get; init; } = true;

    public bool RequireHttpsMetadata { get; init; } = true;
}
