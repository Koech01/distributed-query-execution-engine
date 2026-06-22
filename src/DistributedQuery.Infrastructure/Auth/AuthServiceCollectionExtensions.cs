using DistributedQuery.Core.Interfaces;
using DistributedQuery.Infrastructure.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DistributedQuery.Infrastructure;

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddDistributedQueryAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        InfrastructureHostRole hostRole)
    {
        if (hostRole is not InfrastructureHostRole.Api and not InfrastructureHostRole.Full)
        {
            return services;
        }

        services
            .AddOptions<JwtSigningOptions>()
            .Bind(configuration.GetSection(JwtSigningOptions.SectionName))
            .Validate(static options => !string.IsNullOrWhiteSpace(options.Issuer), "Authentication:JwtSigning:Issuer is required.")
            .Validate(static options => !string.IsNullOrWhiteSpace(options.Audience), "Authentication:JwtSigning:Audience is required.");

        services.AddSingleton<JwtSigningKeyProvider>();

        services
            .AddOptions<EmailAuthOptions>()
            .Bind(configuration.GetSection(EmailAuthOptions.SectionName));

        services
            .AddOptions<GoogleOAuthOptions>()
            .Bind(configuration.GetSection(GoogleOAuthOptions.SectionName));

        services
            .AddOptions<GitHubOAuthOptions>()
            .Bind(configuration.GetSection(GitHubOAuthOptions.SectionName));

        services
            .AddOptions<AuthFrontendOptions>()
            .Bind(configuration.GetSection(AuthFrontendOptions.SectionName));

        services
            .AddOptions<AuthSeedOptions>()
            .Bind(configuration.GetSection(AuthSeedOptions.SectionName));

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IUserRepository, RedisUserRepository>();
        services.AddSingleton<IAuthTokenIssuer, RsaAuthTokenIssuer>();
        services.AddSingleton<IOAuthStateStore, RedisOAuthStateStore>();
        services.AddSingleton<IAuthExchangeCodeStore, RedisAuthExchangeCodeStore>();

        services.AddHttpClient<GoogleOAuthClient>();
        services.AddHttpClient<GitHubOAuthClient>();
        services.AddScoped<OAuthProviderRegistry>();

        return services;
    }
}
