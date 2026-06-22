using System.Security.Cryptography;
using System.Text;
using DistributedQuery.Core.Models;

namespace DistributedQuery.QueryParser.Parsing;

/// <summary>
/// Single source of truth for deterministic plan hash computation.
/// Used by SqlQueryParser (PlanHash field) and CacheKeyBuilder (Redis key suffix).
/// </summary>
public static class PlanHashComputer
{
    public static string ComputeHash(string sql, IReadOnlyList<QueryParameter> parameters)
    {
        var normalized = NormalizeSql(sql);
        var paramSig = BuildParamSignature(parameters);
        var input = $"{normalized}::{paramSig}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string NormalizeSql(string sql)
    {
        var sb = new StringBuilder(sql.Length);
        var prevWasSpace = false;

        foreach (var ch in sql.Trim())
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!prevWasSpace)
                {
                    sb.Append(' ');
                }

                prevWasSpace = true;
            }
            else
            {
                sb.Append(char.ToUpperInvariant(ch));
                prevWasSpace = false;
            }
        }

        return sb.ToString();
    }

    internal static string BuildParamSignature(IReadOnlyList<QueryParameter> parameters) =>
        string.Join(",", parameters
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => $"{p.Name.ToLowerInvariant()}:{p.Type}"));
}
