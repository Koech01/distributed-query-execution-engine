namespace DistributedQuery.Core.Exceptions;

public sealed class QueryParseException : Exception
{
    public string SqlHash { get; }
    public IReadOnlyList<string> ParseErrors { get; }

    public QueryParseException(string message, string sqlHash, IReadOnlyList<string> parseErrors)
        : base(message)
    {
        SqlHash = sqlHash;
        ParseErrors = parseErrors;
    }

    public QueryParseException(string message, string sqlHash, IReadOnlyList<string> parseErrors, Exception inner)
        : base(message, inner)
    {
        SqlHash = sqlHash;
        ParseErrors = parseErrors;
    }
}
