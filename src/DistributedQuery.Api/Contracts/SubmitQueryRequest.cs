using System.ComponentModel.DataAnnotations;
using DistributedQuery.Core.Models;

namespace DistributedQuery.Api.Contracts;

public sealed class SubmitQueryRequest
{
    [Required]
    public string Sql { get; init; } = string.Empty;

    public IReadOnlyList<QueryParameterDto> Parameters { get; init; } = [];

    [Range(1, 120)]
    public int? TimeoutSeconds { get; init; }

    [Range(1, 1_000)]
    public int? MaxNodes { get; init; }

    public bool Async { get; init; }

    public FailurePolicy FailurePolicy { get; init; } = FailurePolicy.BestEffort;

    public Guid? QueryId { get; init; }
}

public sealed class QueryParameterDto
{
    [Required]
    public string Name { get; init; } = string.Empty;

    [Required]
    public string Type { get; init; } = string.Empty;

    [Required]
    public string Value { get; init; } = string.Empty;
}

public sealed record SubmitQueryResponse(
    Guid QueryId,
    string? StatusUrl = null);

public sealed record QueryStatusResponse(
    Guid QueryId,
    string Status,
    string? Message);

public sealed record ErrorResponse(
    string Error,
    string Message,
    IReadOnlyList<string>? Details = null);
