namespace DistributedQuery.Core.Models;

public record QueryPlan(
    Guid PlanId,
    string PlanHash,
    IReadOnlyList<SubQuery> SubQueries,
    MergeInstructions MergeInstructions,
    DateTimeOffset CreatedAt
)
{
    public static QueryPlan Create(
        string planHash,
        IReadOnlyList<SubQuery> subQueries,
        MergeInstructions mergeInstructions) =>
        new(Guid.NewGuid(), planHash, subQueries, mergeInstructions, DateTimeOffset.UtcNow);
}
