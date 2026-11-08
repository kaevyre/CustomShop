using System.Diagnostics.Metrics;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Caching;

/// <summary>Метрики кешу агрегованих відповідей (Meter додано в Platform.ServiceDefaults).</summary>
public sealed class AggregatorCacheMetrics
{
    private const string Level = "L2";

    private readonly Counter<long> _hits;
    private readonly Counter<long> _misses;

    public AggregatorCacheMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Platform.Aggregator.Cache");
        _hits = meter.CreateCounter<long>("cache.hits", description: "Aggregated response cache hits");
        _misses = meter.CreateCounter<long>("cache.misses", description: "Aggregated response cache misses");
    }

    public void Hit() => _hits.Add(1, new KeyValuePair<string, object?>("cache.level", Level));

    public void Miss() => _misses.Add(1, new KeyValuePair<string, object?>("cache.level", Level));
}

/// <summary>
/// Кеш повних карток замовлення в Redis. TTL короткий (30 с) і менший за TTL кешів окремих сервісів,
/// інакше картка виглядала б свіжішою за дані, з яких зібрана. Часткові відповіді не кешуються.
/// Збій Redis не впливає на відповідь: картка просто збирається заново.
/// </summary>
public sealed class OrderCardCache(IDistributedCache cache, AggregatorCacheMetrics metrics, ILogger<OrderCardCache> logger)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(15);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private long _retryAt;

    private bool IsAvailable => Environment.TickCount64 >= Interlocked.Read(ref _retryAt);

    public static string Key(long orderId) => $"aggregator:order:{orderId}:full";

    public async Task<FullOrderDto?> GetAsync(long orderId, CancellationToken ct)
    {
        var key = Key(orderId);
        if (!IsAvailable)
        {
            metrics.Miss();
            return null;
        }

        try
        {
            var bytes = await cache.GetAsync(key, ct);
            if (bytes is not null)
            {
                metrics.Hit();
                logger.LogInformation("Cache hit {CacheKey} at {CacheLevel}, size {SizeBytes} bytes", key, "L2", bytes.Length);
                return JsonSerializer.Deserialize<FullOrderDto>(bytes, Json);
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Reading {CacheKey} from Redis failed, the card will be built from services", key);
            PauseRedis();
        }

        metrics.Miss();
        logger.LogInformation("Cache miss {CacheKey} at {CacheLevel}", key, "L2");
        return null;
    }

    public async Task SetAsync(FullOrderDto card, CancellationToken ct)
    {
        var key = Key(card.OrderId);
        if (card.Warnings.Count > 0)
        {
            logger.LogInformation("Partial card {CacheKey} is not cached, {WarningsCount} warnings", key, card.Warnings.Count);
            return;
        }

        if (!IsAvailable)
            return;

        try
        {
            await cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(card, Json),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl }, ct);
            logger.LogInformation("Cached {CacheKey} for {TtlSeconds} s", key, Ttl.TotalSeconds);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Writing {CacheKey} to Redis failed", key);
            PauseRedis();
        }
    }

    // не чекаємо тайм-аут Redis у кожному запиті, поки він недоступний
    private void PauseRedis() =>
        Interlocked.Exchange(ref _retryAt, Environment.TickCount64 + (long)RetryDelay.TotalMilliseconds);
}
