namespace DistributedQuery.Infrastructure.Messaging;

public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    public bool EnableWorkerConsumer { get; init; } = false;
    public bool EnableCoordinatorConsumer { get; init; } = false;
    public IReadOnlyList<int> WorkerShardIndices { get; init; } = Array.Empty<int>();
    public int WorkerPrefetchCount { get; init; } = 5;
    public int CoordinatorPrefetchCount { get; init; } = 100;
    public int ResultRendezvousTtlHours { get; init; } = 24;
    public int MergeLockTtlSeconds { get; init; } = 30;
}
