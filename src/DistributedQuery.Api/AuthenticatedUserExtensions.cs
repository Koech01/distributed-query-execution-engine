using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace DistributedQuery.Api;

public static class AuthenticatedUserExtensions
{
    public static string? GetUserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
}
