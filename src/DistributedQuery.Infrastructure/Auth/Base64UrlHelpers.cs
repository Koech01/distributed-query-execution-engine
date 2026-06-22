namespace DistributedQuery.Infrastructure.Auth;

internal static class Base64UrlHelpers
{
    public static string Encode(byte[] input) =>
        Convert.ToBase64String(input).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
