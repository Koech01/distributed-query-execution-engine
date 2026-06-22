using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DistributedQuery.Core.Interfaces;
using DistributedQuery.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DistributedQuery.Infrastructure.Auth;

public sealed class RsaAuthTokenIssuer : IAuthTokenIssuer
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Auth.TokenIssuer");

    private readonly JwtSigningOptions _options;
    private readonly JwtSigningKeyProvider _keyProvider;
    private readonly ILogger<RsaAuthTokenIssuer> _logger;

    public RsaAuthTokenIssuer(
        IOptions<JwtSigningOptions> options,
        JwtSigningKeyProvider keyProvider,
        ILogger<RsaAuthTokenIssuer> logger)
    {
        _options = options.Value;
        _keyProvider = keyProvider;
        _logger = logger;
    }

    public AuthTokenResult IssueAccessToken(UserAccount user, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("RsaAuthTokenIssuer.IssueAccessToken", ActivityKind.Internal);
        activity?.SetTag("auth.user_id", user.UserId);

        var now = DateTimeOffset.UtcNow;
        var expires = now.AddSeconds(_options.AccessTokenLifetimeSeconds);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.DisplayName ?? user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };

        claims.AddRange(user.Scopes.Select(scope => new Claim("scope", scope)));

        var credentials = new SigningCredentials(_keyProvider.SigningKey, SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer.TrimEnd('/') + "/",
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        _logger.LogInformation("Issued access token for user {UserId}", user.UserId);

        return new AuthTokenResult(accessToken, _options.AccessTokenLifetimeSeconds);
    }
}

public sealed class JwtJwksDocument
{
    public required IReadOnlyList<JwtJwksKey> Keys { get; init; }
}

public sealed class JwtJwksKey
{
    public required string Kty { get; init; }

    public required string Use { get; init; }

    public required string Alg { get; init; }

    public required string Kid { get; init; }

    public required string N { get; init; }

    public required string E { get; init; }
}

public static class JwtJwksProvider
{
    public const string KeyId = "dqee-signing-key";

    public static JwtJwksDocument CreateDocument(RsaSecurityKey signingKey)
    {
        var parameters = signingKey.Rsa?.ExportParameters(false)
            ?? throw new InvalidOperationException("Signing key must be RSA.");

        return new JwtJwksDocument
        {
            Keys =
            [
                new JwtJwksKey
                {
                    Kty = "RSA",
                    Use = "sig",
                    Alg = SecurityAlgorithms.RsaSha256,
                    Kid = KeyId,
                    N = Base64UrlEncoder.Encode(parameters.Modulus!),
                    E = Base64UrlEncoder.Encode(parameters.Exponent!),
                }
            ]
        };
    }
}
