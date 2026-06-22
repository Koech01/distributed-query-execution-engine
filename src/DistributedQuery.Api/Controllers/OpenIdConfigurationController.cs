using DistributedQuery.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DistributedQuery.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route(".well-known")]
public sealed class OpenIdConfigurationController : ControllerBase
{
    private readonly JwtSigningKeyProvider _keyProvider;
    private readonly JwtSigningOptions _options;

    public OpenIdConfigurationController(JwtSigningKeyProvider keyProvider, Microsoft.Extensions.Options.IOptions<JwtSigningOptions> options)
    {
        _keyProvider = keyProvider;
        _options = options.Value;
    }

    [HttpGet("openid-configuration")]
    public IActionResult GetOpenIdConfiguration()
    {
        var issuer = _options.Issuer.TrimEnd('/') + "/";
        return Ok(new
        {
            issuer,
            jwks_uri = $"{issuer}.well-known/jwks",
        });
    }

    [HttpGet("jwks")]
    public IActionResult GetJwks()
    {
        return Ok(_keyProvider.CreateJwksDocument());
    }
}
