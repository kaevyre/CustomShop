using System.Diagnostics.Metrics;
using Microsoft.Extensions.Caching.Memory;

namespace Reviews.Application.Caching;

/// <summary>
/// Метрики кешу (Meter "Reviews.Cache", підключений в Platform.ServiceDefaults → вкладка Metrics у Dashboard):
/// cache.hits / cache.misses з тегом cache.level (L1/L2), hit ratio за рівнями, інвалідації, витіснення,
/// тривалість операцій і заповнення кешу в пам'яті.
/// </summary>
public sealed class CacheMetrics
{
    public const string MeterName = "Reviews.Cache";

    private readonly Counter<long> _hits;
    private readonly Counter<long> _misses;
    private readonly Counter<long> _invalidations;
    private readonly Counter<long> _evictions;
    private readonly Histogram<double> _duration;

    private long _l1Hits, _l1Misses, _l2Hits, _l2Misses;

    public CacheMetrics(IMeterFactory meterFactory, IMemoryCache memory)
    {
        var meter = meterFactory.Create(MeterName);
        _hits = meter.CreateCounter<long>("cache.hits", description: "Cache hits by level");
        _misses = meter.CreateCounter<long>("cache.misses", description: "Cache misses by level");
        _invalidations = meter.CreateCounter<long>("cache.invalidations", description: "Invalidated keys and versions");
        _evictions = meter.CreateCounter<long>("cache.evictions", description: "L1 entries evicted or expired");
        _duration = meter.CreateHistogram<double>("cache.operation.duration", unit: "ms", description: "Cache read duration");

        meter.CreateObservableGauge("cache.hit_ratio", ObserveHitRatio, description: "Hit ratio by level");
        meter.CreateObservableGauge("cache.memory.entries", () => memory.GetCurrentStatistics()?.CurrentEntryCount ?? 0,
            description: "Entries in L1");
        meter.CreateObservableGauge("cache.memory.size", () => memory.GetCurrentStatistics()?.CurrentEstimatedSize ?? 0,
            description: "Estimated size of L1 in Size units");
    }

    public void Hit(string level)
    {
        Interlocked.Increment(ref level == "L1" ? ref _l1Hits : ref _l2Hits);
        _hits.Add(1, new KeyValuePair<string, object?>("cache.level", level));
    }

    public void Miss(string level)
    {
        Interlocked.Increment(ref level == "L1" ? ref _l1Misses : ref _l2Misses);
        _misses.Add(1, new KeyValuePair<string, object?>("cache.level", level));
    }

    public void Invalidated(string reason) =>
        _invalidations.Add(1, new KeyValuePair<string, object?>("cache.reason", reason));

    public void Evicted(string reason) =>
        _evictions.Add(1, new KeyValuePair<string, object?>("cache.reason", reason));

    public void Duration(string level, double milliseconds) =>
        _duration.Record(milliseconds, new KeyValuePair<string, object?>("cache.level", level));

    private IEnumerable<Measurement<double>> ObserveHitRatio()
    {
        yield return Ratio("L1", Interlocked.Read(ref _l1Hits), Interlocked.Read(ref _l1Misses));
        yield return Ratio("L2", Interlocked.Read(ref _l2Hits), Interlocked.Read(ref _l2Misses));
    }

    private static Measurement<double> Ratio(string level, long hits, long misses) => new(
        hits + misses == 0 ? 0 : (double)hits / (hits + misses),
        new KeyValuePair<string, object?>("cache.level", level));
}
