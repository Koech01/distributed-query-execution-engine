using System.Collections.Concurrent;
using System.Diagnostics;
using DistributedQuery.Core.Models;

namespace DistributedQuery.Coordinator;

public sealed class ActiveQueryRegistry
{
    private static readonly ActivitySource ActivitySource = new("DistributedQuery.Coordinator.ActiveQueryRegistry");

    private readonly ConcurrentDictionary<Guid, ActiveQueryEntry> _active = new();

    public ActiveQueryScope BeginQuery(
        Guid queryId,
        ActiveQueryKind kind,
        string planHash,
        int subQueryCount)
    {
        var entry = new ActiveQueryEntry(
            queryId,
            kind,
            planHash,
            subQueryCount,
            DateTimeOffset.UtcNow,
            new CancellationTokenSource());

        if (!_active.TryAdd(queryId, entry))
        {
            entry.CancellationSource.Dispose();
            throw new InvalidOperationException($"Query {queryId:D} is already active.");
        }

        using var activity = ActivitySource.StartActivity("coordinator.active_query.register", ActivityKind.Internal);
        activity?.SetTag("query.id", queryId.ToString("D"));
        activity?.SetTag("query.kind", kind.ToString());

        return new ActiveQueryScope(this, queryId, entry.CancellationSource);
    }

    public ActiveQueryPage List(int limit, int offset)
    {
        var clampedLimit = Math.Clamp(limit, 1, 200);
        var clampedOffset = Math.Max(0, offset);

        var ordered = _active.Values
            .OrderByDescending(static entry => entry.StartedAt)
            .ToArray();

        var page = ordered
            .Skip(clampedOffset)
            .Take(clampedLimit)
            .Select(static entry => new ActiveQuerySummary(
                entry.QueryId,
                entry.Kind,
                entry.PlanHash,
                entry.SubQueryCount,
                entry.StartedAt,
                entry.CancellationSource.IsCancellationRequested))
            .ToArray();

        return new ActiveQueryPage(page, ordered.Length, clampedLimit, clampedOffset);
    }

    public CancelQueryResult Cancel(Guid queryId)
    {
        if (!_active.TryGetValue(queryId, out var entry))
        {
            return new CancelQueryResult(
                queryId,
                Found: false,
                CancellationRequested: false,
                "No active query found.");
        }

        if (!entry.CancellationSource.IsCancellationRequested)
        {
            entry.CancellationSource.Cancel();
        }

        using var activity = ActivitySource.StartActivity("coordinator.active_query.cancel", ActivityKind.Internal);
        activity?.SetTag("query.id", queryId.ToString("D"));

        return new CancelQueryResult(
            queryId,
            Found: true,
            CancellationRequested: true,
            "Cancellation requested.");
    }

    internal void Complete(Guid queryId)
    {
        if (_active.TryRemove(queryId, out var entry))
        {
            entry.CancellationSource.Dispose();
        }
    }

    public int ActiveCount => _active.Count;

    private sealed class ActiveQueryEntry
    {
        public ActiveQueryEntry(
            Guid queryId,
            ActiveQueryKind kind,
            string planHash,
            int subQueryCount,
            DateTimeOffset startedAt,
            CancellationTokenSource cancellationSource)
        {
            QueryId = queryId;
            Kind = kind;
            PlanHash = planHash;
            SubQueryCount = subQueryCount;
            StartedAt = startedAt;
            CancellationSource = cancellationSource;
        }

        public Guid QueryId { get; }
        public ActiveQueryKind Kind { get; }
        public string PlanHash { get; }
        public int SubQueryCount { get; }
        public DateTimeOffset StartedAt { get; }
        public CancellationTokenSource CancellationSource { get; }
    }
}

public sealed class ActiveQueryScope : IDisposable
{
    private readonly ActiveQueryRegistry _registry;
    private readonly Guid _queryId;
    private readonly CancellationTokenSource _cancellationSource;
    private int _disposed;

    internal ActiveQueryScope(
        ActiveQueryRegistry registry,
        Guid queryId,
        CancellationTokenSource cancellationSource)
    {
        _registry = registry;
        _queryId = queryId;
        _cancellationSource = cancellationSource;
    }

    public CancellationToken CancellationToken => _cancellationSource.Token;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _registry.Complete(_queryId);
    }
}
