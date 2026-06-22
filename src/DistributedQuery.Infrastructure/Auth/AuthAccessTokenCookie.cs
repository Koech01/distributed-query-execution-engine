using DistributedQuery.Core.Models;
using Microsoft.AspNetCore.Http;

namespace DistributedQuery.Infrastructure.Auth;

public static class AuthAccessTokenCookie
{
    public const string CookieName = "dqee_access_token";

    public static void Append(HttpContext httpContext, AuthTokenResult token)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(token);

        httpContext.Response.Cookies.Append(
            CookieName,
            token.AccessToken,
            CreateCookieOptions(httpContext, token.ExpiresInSeconds));
    }

    public static void Delete(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/" });
    }

    private static CookieOptions CreateCookieOptions(HttpContext httpContext, int expiresInSeconds)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = httpContext.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = TimeSpan.FromSeconds(Math.Max(expiresInSeconds, 1)),
        };
    }
}
