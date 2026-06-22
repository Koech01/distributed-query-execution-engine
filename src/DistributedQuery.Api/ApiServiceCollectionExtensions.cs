using System.Security.Claims;
using DistributedQuery.Api.Middleware;
using DistributedQuery.Api.Options;
using DistributedQuery.Api.Services;
using DistributedQuery.Infrastructure;
using DistributedQuery.Infrastructure.Auth;
using DistributedQuery.Infrastructure.Coordinator;
using DistributedQuery.Infrastructure.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DistributedQuery.Api;

public static class ApiServiceCollectionExtensions
{
    public const string QueryReadPolicy = "QueryRead";
    public const string QueryAdminPolicy = "QueryAdmin";

    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<ApiOptions>()
            .Bind(configuration.GetSection(ApiOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<AuthenticationOptions>()
            .Bind(configuration.GetSection(AuthenticationOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection(RateLimitingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<OpenTelemetryOptions>()
            .Bind(configuration.GetSection(OpenTelemetryOptions.SectionName));

        services.AddInfrastructure(configuration, InfrastructureHostRole.Api);
        services.AddSingleton<RequestRateLimiter>();
        services.AddSingleton<ApiHealthService>();
        services.AddScoped<AuthenticationService>();
        services.AddScoped<AdminDashboardService>();
        services.AddHostedService<DevelopmentAccountSeeder>();
        services.AddAuthRateLimiting(configuration);

        services.AddHttpClient<CoordinatorClientHealthProbe>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<CoordinatorClientOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddAuthorization(options =>
        {
            options.AddPolicy(QueryReadPolicy, policy =>
                policy.RequireAssertion(context => AuthorizeScope(context, "query:read", "query:admin")));

            options.AddPolicy(QueryAdminPolicy, policy =>
                policy.RequireAssertion(context => AuthorizeScope(context, "query:admin")));
        });

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthenticationOptions>, JwtSigningKeyProvider>((jwtOptions, authOptions, keyProvider) =>
            {
                jwtOptions.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (string.IsNullOrEmpty(context.Token) &&
                            context.Request.Cookies.TryGetValue(AuthAccessTokenCookie.CookieName, out var accessToken))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                };

                var auth = authOptions.Value;
                if (!auth.Enabled)
                {
                    jwtOptions.Authority = null;
                    jwtOptions.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ValidateLifetime = false,
                        ValidateIssuerSigningKey = false,
                        SignatureValidator = static (token, _) => new JsonWebToken(token)
                    };
                    return;
                }

                if (auth.UseLocalJwtIssuer)
                {
                    var signingOptions = configuration.GetSection(JwtSigningOptions.SectionName).Get<JwtSigningOptions>()
                        ?? new JwtSigningOptions();

                    jwtOptions.Authority = null;
                    jwtOptions.RequireHttpsMetadata = auth.RequireHttpsMetadata;
                    jwtOptions.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = signingOptions.Issuer.TrimEnd('/') + "/",
                        ValidAudience = signingOptions.Audience,
                        IssuerSigningKey = keyProvider.ValidationKey,
                        ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    };
                    return;
                }

                jwtOptions.Authority = auth.Authority;
                jwtOptions.Audience = auth.Audience;
                jwtOptions.RequireHttpsMetadata = auth.RequireHttpsMetadata;
                jwtOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
                };
            });

        return services;
    }

    private static bool AuthorizeScope(AuthorizationHandlerContext context, params string[] allowedScopes)
    {
        if (context.Resource is not HttpContext httpContext)
        {
            return false;
        }

        var enabled = httpContext.RequestServices
            .GetRequiredService<IOptions<AuthenticationOptions>>()
            .Value
            .Enabled;

        if (!enabled)
        {
            return true;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var scopeClaims = context.User.FindAll("scope").Select(static claim => claim.Value);
        var roleClaims = context.User.FindAll(ClaimTypes.Role).Select(static claim => claim.Value);
        return allowedScopes.Any(scope => scopeClaims.Contains(scope) || roleClaims.Contains(scope));
    }
}
