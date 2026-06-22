using DistributedQuery.Core.Exceptions;

namespace DistributedQuery.QueryParser.Parsing;

/// <summary>
/// Resolves which shard indices a query must target based on WHERE predicates and the shard map.
/// Returns all shard indices when no shard key predicate is present (scatter-gather).
/// </summary>
public sealed class ShardTargetResolver
{
    private readonly ShardMapOptions _options;

    public ShardTargetResolver(ShardMapOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Returns the distinct shard indices that must be queried.
    /// </summary>
    public IReadOnlyList<int> Resolve(string tableName, ShardKeyPredicate? predicate)
    {
        if (!_options.Tables.TryGetValue(tableName, out var config))
            throw new ShardConfigurationException(tableName);

        if (predicate is null)
            return Enumerable.Range(0, config.ShardCount).ToList();

        return config.Strategy.ToUpperInvariant() switch
        {
            "CONSISTENTHASH" => ResolveHash(predicate, config),
            "RANGEPARTITION"  => ResolveRange(predicate, config),
            _ => Enumerable.Range(0, config.ShardCount).ToList()
        };
    }

    private static IReadOnlyList<int> ResolveHash(ShardKeyPredicate predicate, TableShardConfig config)
    {
        switch (predicate.Type)
        {
            case ShardPredicateType.Equality when predicate.EqualityValue is not null:
            {
                var shard = MurmurHash3(predicate.EqualityValue) % config.ShardCount;
                return [shard];
            }

            case ShardPredicateType.InList when predicate.InValues is not null:
            {
                return predicate.InValues
                    .Select(v => MurmurHash3(v) % config.ShardCount)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();
            }

            // Hash partitioning cannot prune range predicates
            default:
                return Enumerable.Range(0, config.ShardCount).ToList();
        }
    }

    private static IReadOnlyList<int> ResolveRange(ShardKeyPredicate predicate, TableShardConfig config)
    {
        if (config.Ranges is null or { Count: 0 })
            return Enumerable.Range(0, config.ShardCount).ToList();

        switch (predicate.Type)
        {
            case ShardPredicateType.Equality when predicate.EqualityValue is not null:
            {
                var value = predicate.EqualityValue.Trim('\'');
                var shard = FindRangeShard(value, config);
                return shard.HasValue ? [shard.Value] : Enumerable.Range(0, config.ShardCount).ToList();
            }

            case ShardPredicateType.Range:
            {
                var min = predicate.RangeMin?.Trim('\'');
                var max = predicate.RangeMax?.Trim('\'');
                return FindOverlappingShards(min, max, config);
            }

            case ShardPredicateType.InList when predicate.InValues is not null:
            {
                return predicate.InValues
                    .Select(v => FindRangeShard(v.Trim('\''), config))
                    .Where(s => s.HasValue)
                    .Select(s => s!.Value)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();
            }

            default:
                return Enumerable.Range(0, config.ShardCount).ToList();
        }
    }

    private static int? FindRangeShard(string value, TableShardConfig config)
    {
        foreach (var range in config.Ranges!)
        {
            var aboveMin = range.Min is null || string.Compare(value, range.Min, StringComparison.Ordinal) >= 0;
            var belowMax = range.Max is null || string.Compare(value, range.Max, StringComparison.Ordinal) <= 0;
            if (aboveMin && belowMax) return range.Shard;
        }
        return null;
    }

    private static IReadOnlyList<int> FindOverlappingShards(string? queryMin, string? queryMax, TableShardConfig config)
    {
        var result = new List<int>();
        foreach (var range in config.Ranges!)
        {
            // Range overlaps if: range.Min <= queryMax AND range.Max >= queryMin
            var rangeStartsBeforeQueryEnd = queryMax is null || range.Min is null
                || string.Compare(range.Min, queryMax, StringComparison.Ordinal) <= 0;
            var rangeEndsAfterQueryStart = queryMin is null || range.Max is null
                || string.Compare(range.Max, queryMin, StringComparison.Ordinal) >= 0;

            if (rangeStartsBeforeQueryEnd && rangeEndsAfterQueryStart)
                result.Add(range.Shard);
        }
        return result.Count > 0 ? result : Enumerable.Range(0, config.ShardCount).ToList();
    }

    // MurmurHash3 finalizer (32-bit) - deterministic, fast, good distribution
    private static int MurmurHash3(string key)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(key);
        uint h = 0;
        foreach (var b in bytes)
        {
            h ^= b;
            h *= 0x5bd1e995;
            h ^= h >> 15;
        }
        h ^= h >> 13;
        h *= 0x5bd1e995;
        h ^= h >> 15;
        return (int)(h & 0x7FFFFFFF); // keep positive
    }
}
