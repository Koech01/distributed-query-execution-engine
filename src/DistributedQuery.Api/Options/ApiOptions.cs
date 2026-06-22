using System.ComponentModel.DataAnnotations;

namespace DistributedQuery.Api.Options;

public sealed class ApiOptions
{
    public const string SectionName = "Api";

    [Range(1, 10_000)]
    public int MaxSqlLengthChars { get; init; } = 10_000;

    [Range(1, 100)]
    public int MaxParameters { get; init; } = 50;

    [Range(1, 120)]
    public int MinTimeoutSeconds { get; init; } = 1;

    [Range(1, 120)]
    public int MaxTimeoutSeconds { get; init; } = 120;

    [Range(1, 1_000)]
    public int MaxNodes { get; init; } = 1_000;
}
