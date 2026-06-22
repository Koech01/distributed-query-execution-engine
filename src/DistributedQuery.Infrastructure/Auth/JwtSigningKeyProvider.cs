using System.Diagnostics;
using System.Security.Cryptography;
using DistributedQuery.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DistributedQuery.Infrastructure.Auth;

public sealed class JwtSigningKeyProvider
{
    private readonly RsaSecurityKey _signingKey;
    private readonly RsaSecurityKey _validationKey;

    public JwtSigningKeyProvider(IOptions<JwtSigningOptions> options, IHostEnvironment environment)
    {
        var jwtOptions = options.Value;

        if (!string.IsNullOrWhiteSpace(jwtOptions.PrivateKeyPem))
        {
            var signingRsa = RSA.Create();
            signingRsa.ImportFromPem(jwtOptions.PrivateKeyPem);
            _signingKey = new RsaSecurityKey(signingRsa) { KeyId = JwtJwksProvider.KeyId };

            if (!string.IsNullOrWhiteSpace(jwtOptions.PublicKeyPem))
            {
                var validationRsa = RSA.Create();
                validationRsa.ImportFromPem(jwtOptions.PublicKeyPem);
                _validationKey = new RsaSecurityKey(validationRsa) { KeyId = JwtJwksProvider.KeyId };
            }
            else
            {
                _validationKey = _signingKey;
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(jwtOptions.PublicKeyPem))
        {
            var validationRsa = RSA.Create();
            validationRsa.ImportFromPem(jwtOptions.PublicKeyPem);
            _validationKey = new RsaSecurityKey(validationRsa) { KeyId = JwtJwksProvider.KeyId };
            throw new InvalidOperationException("Authentication:JwtSigning:PrivateKeyPem is required to issue access tokens.");
        }

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Authentication:JwtSigning requires configured RSA keys outside Development.");
        }

        var generated = RSA.Create(2048);
        _signingKey = new RsaSecurityKey(generated) { KeyId = JwtJwksProvider.KeyId };
        _validationKey = _signingKey;
    }

    public RsaSecurityKey SigningKey => _signingKey;

    public RsaSecurityKey ValidationKey => _validationKey;

    public JwtJwksDocument CreateJwksDocument() => JwtJwksProvider.CreateDocument(_signingKey);
}
