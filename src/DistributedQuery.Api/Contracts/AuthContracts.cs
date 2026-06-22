using System.ComponentModel.DataAnnotations;

namespace DistributedQuery.Api.Contracts;

public sealed class RegisterRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MinLength(12)]
    public string Password { get; init; } = string.Empty;

    [Required]
    [MinLength(2)]
    [MaxLength(100)]
    public string DisplayName { get; init; } = string.Empty;
}

public sealed class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public sealed class AuthTokenResponse
{
    public required string AccessToken { get; init; }

    public required int ExpiresIn { get; init; }

    public required string TokenType { get; init; }
}

public sealed class ExchangeTokenRequest
{
    [Required]
    public string ExchangeCode { get; init; } = string.Empty;
}

public sealed class UpdateProfileRequest
{
    [MinLength(2)]
    [MaxLength(100)]
    public string? DisplayName { get; init; }

    [EmailAddress]
    public string? Email { get; init; }
}

public sealed class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required]
    [MinLength(12)]
    public string NewPassword { get; init; } = string.Empty;
}

public sealed class UserProfileResponse
{
    public required string UserId { get; init; }

    public required string Email { get; init; }

    public string? DisplayName { get; init; }

    public required bool HasPasswordLogin { get; init; }

    public required IReadOnlyList<string> LinkedProviders { get; init; }

    public required IReadOnlyList<string> Scopes { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}

public sealed class UpdateProfileResponse
{
    public required UserProfileResponse Profile { get; init; }

    public AuthTokenResponse? Token { get; init; }
}
