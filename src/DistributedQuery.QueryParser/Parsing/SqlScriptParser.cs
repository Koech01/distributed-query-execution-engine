using DistributedQuery.Core.Exceptions;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace DistributedQuery.QueryParser.Parsing;

/// <summary>
/// Parses SQL once and returns a validated TSqlScript.
/// </summary>
public static class SqlScriptParser
{
    private const int DefaultMaxComplexityScore = 500;

    public static TSqlScript ParseAndValidate(string sql, int maxLengthChars = 10_000, int maxComplexityScore = DefaultMaxComplexityScore)
    {
        QueryValidator.ValidatePreParse(sql, maxLengthChars);

        var parser = new TSql160Parser(initialQuotedIdentifiers: false);
        var fragment = parser.Parse(new StringReader(sql), out var errors);

        QueryValidator.ValidateParseResult(fragment, errors);
        QueryValidator.ValidateComplexity(fragment, maxComplexityScore);

        return (TSqlScript)fragment;
    }
}
