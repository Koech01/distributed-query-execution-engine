namespace DistributedQuery.Core.Messages;

public record QueryCompleted(
    Guid QueryId,
    bool Success,
    string? ErrorMessage,
    int SchemaVersion = 1
);
