using DistributedQuery.Core.Exceptions;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace DistributedQuery.QueryParser.Parsing;

/// <summary>
/// Validates SQL before parsing. Whitelist-based: only SELECT statements pass.
/// Rejects disallowed statement types and dangerous function names.
/// </summary>
public static class QueryValidator
{
    private static readonly HashSet<string> BlockedTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "xp_cmdshell", "openrowset", "opendatasource", "openquery", "bulk insert"
    };

    public static void Validate(string sql, int maxLengthChars = 10_000)
    {
        ValidatePreParse(sql, maxLengthChars);

        var parser = new TSql160Parser(initialQuotedIdentifiers: false);
        var fragment = parser.Parse(new StringReader(sql), out var errors);

        ValidateParseResult(fragment, errors);
        ValidateComplexity(fragment);
    }

    internal static void ValidatePreParse(string sql, int maxLengthChars = 10_000)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new QueryParseException(
                "SQL must not be empty",
                string.Empty,
                ["SQL is null or whitespace"]);
        }

        if (sql.Length > maxLengthChars)
        {
            throw new QueryParseException(
                $"SQL exceeds maximum length of {maxLengthChars} characters",
                string.Empty,
                [$"SQL length {sql.Length} exceeds limit {maxLengthChars}"]);
        }

        foreach (var token in BlockedTokens)
        {
            if (ContainsBlockedToken(sql, token))
            {
                throw new QueryParseException(
                    $"SQL contains disallowed token: '{token}'",
                    string.Empty,
                    [$"Blocked token detected: {token}"]);
            }
        }
    }

    internal static void ValidateParseResult(object? fragment, IList<ParseError> errors)
    {
        if (errors.Count > 0)
        {
            var messages = errors
                .Select(e => $"Line {e.Line}, Col {e.Column}: {e.Message}")
                .ToList();
            throw new QueryParseException("SQL contains parse errors", string.Empty, messages);
        }

        if (fragment is not TSqlScript script)
        {
            throw new QueryParseException("SQL produced no parseable script", string.Empty, ["Empty script"]);
        }

        foreach (var batch in script.Batches)
        {
            foreach (var statement in batch.Statements)
            {
                if (statement is not SelectStatement)
                {
                    throw new QueryParseException(
                        $"Statement type '{statement.GetType().Name}' is not permitted. Only SELECT is allowed.",
                        string.Empty,
                        [$"Disallowed statement: {statement.GetType().Name}"]);
                }
            }
        }
    }

    internal static void ValidateComplexity(object? fragment, int maxComplexityScore = 500)
    {
        if (fragment is not TSqlScript script)
        {
            return;
        }

        var counter = new ComplexityCounter();
        script.Accept(counter);

        if (counter.Score > maxComplexityScore)
        {
            throw new QueryParseException(
                $"SQL exceeds maximum complexity score of {maxComplexityScore}",
                string.Empty,
                [$"Complexity score {counter.Score} exceeds limit {maxComplexityScore}"]);
        }
    }

    private static bool ContainsBlockedToken(string sql, string token)
    {
        var index = 0;
        while ((index = sql.IndexOf(token, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            if (IsTokenBoundary(sql, index, token.Length))
            {
                return true;
            }

            index += token.Length;
        }

        return false;
    }

    private static bool IsTokenBoundary(string sql, int start, int length)
    {
        var before = start > 0 ? sql[start - 1] : ' ';
        var after = start + length < sql.Length ? sql[start + length] : ' ';
        return !char.IsLetterOrDigit(before) && !char.IsLetterOrDigit(after);
    }

    private sealed class ComplexityCounter : TSqlFragmentVisitor
    {
        public int Score { get; private set; }

        public override void Visit(TSqlFragment node)
        {
            Score++;
            base.Visit(node);
        }
    }
}
