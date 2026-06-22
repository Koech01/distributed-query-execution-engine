namespace DistributedQuery.Api.Middleware;

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Content-Security-Policy"] = "default-src 'none'";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = string.Empty;

        await _next(context).ConfigureAwait(false);
    }
}
