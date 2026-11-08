using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Catalog.Bll.Caching;

/// <summary>Терміни життя запису за типом даних. L1 завжди коротший за L2, інакше застарілі дані переживуть оновлення в Redis.</summary>
public sealed record CacheProfile(
    TimeSpan L1Sliding,
    TimeSpan L1Absolute,
    TimeSpan L2Ttl,
    long Size = 1,
    CacheItemPriority Priority = CacheItemPriority.Normal);

public interface ITwoLevelCache
{
    /// <summary>Cache-aside: L1 (пам'ять) → L2 (Redis) → factory (база даних); рівні заповнюються на зворотному шляху.</summary>
    Task<T?> GetOrCreateAsync<T>(string key, CacheProfile profile, Func<CancellationToken, Task<T?>> factory, CancellationToken ct);

    /// <summary>Синхронно з операцією запису видаляє ключі з обох рівнів.</summary>
    Task InvalidateAsync(string reason, params string[] keys);

    /// <summary>Поточна версія групи ключів (масова інвалідація списків без перебору ключів).</summary>
    Task<string> GetVersionAsync(string versionKey, CancellationToken ct);

    /// <summary>Змінює версію групи: усі ключі зі старою версією стають недосяжними.</summary>
    Task BumpVersionAsync(string versionKey, string reason);
}

/// <summary>
/// Дворівневий кеш: IMemoryCache (L1, в межах екземпляра) + IDistributedCache поверх Redis (L2, спільний для реплік).
/// Збій Redis не зупиняє сервіс: L2 тимчасово пропускається, а інвалідації, які не вдалося виконати, застосовуються
/// щойно Redis повернеться, щоб після простою не читати застарілі дані.
/// </summary>
public sealed class TwoLevelCache(
    IMemoryCache memory,
    IDistributedCache distributed,
    CacheMetrics metrics,
    ILogger<TwoLevelCache> logger) : ITwoLevelCache
{
    private const string L1 = "L1";
    private const string L2 = "L2";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan L2RetryDelay = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan VersionTtl = TimeSpan.FromDays(30);

    private readonly ConcurrentDictionary<string, byte> _pendingRemovals = new();
    private readonly ConcurrentDictionary<string, byte> _pendingBumps = new();
    private readonly ConcurrentDictionary<string, long> _localVersions = new();
    private long _l2RetryAt;

    private bool IsL2Available => Environment.TickCount64 >= Interlocked.Read(ref _l2RetryAt);

    public async Task<T?> GetOrCreateAsync<T>(
        string key, CacheProfile profile, Func<CancellationToken, Task<T?>> factory, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        if (memory.TryGetValue(key, out T? value))
        {
            metrics.Hit(L1);
            metrics.Duration(L1, sw.Elapsed.TotalMilliseconds);
            logger.LogDebug("Cache hit {CacheKey} at {CacheLevel}, ttl {TtlSeconds} s, size {CacheSize} units",
                key, L1, profile.L1Absolute.TotalSeconds, profile.Size);
            return value;
        }

        metrics.Miss(L1);
        logger.LogDebug("Cache miss {CacheKey} at {CacheLevel}", key, L1);

        var bytes = await ReadL2Async(key, ct);
        if (bytes is not null && TryDeserialize(key, bytes, out value))
        {
            metrics.Hit(L2);
            logger.LogDebug("Cache hit {CacheKey} at {CacheLevel}, ttl {TtlSeconds} s, size {SizeBytes} bytes",
                key, L2, profile.L2Ttl.TotalSeconds, bytes.Length);
            SetL1(key, value, profile);
            return value;
        }

        metrics.Miss(L2);
        logger.LogDebug("Cache miss {CacheKey} at {CacheLevel}, loading from database", key, L2);

        value = await factory(ct);
        if (value is null)
            return default;

        await WriteL2Async(key, JsonSerializer.SerializeToUtf8Bytes(value, Json), profile.L2Ttl, ct);
        SetL1(key, value, profile);
        return value;
    }

    public async Task InvalidateAsync(string reason, params string[] keys)
    {
        foreach (var key in keys)
        {
            memory.Remove(key);
            metrics.Invalidated(reason);

            // без токена запиту: дані вже змінені, тож інвалідація має дійти до кінця навіть якщо клієнт відключився
            if (await TryRemoveL2Async(key))
                logger.LogInformation("Cache invalidated {CacheKey}, reason {Reason}", key, reason);
        }
    }

    public async Task<string> GetVersionAsync(string versionKey, CancellationToken ct)
    {
        if (IsL2Available)
        {
            var bytes = await ReadL2Async(versionKey, ct);
            if (IsL2Available)
                return bytes is null ? "0" : Encoding.UTF8.GetString(bytes);
        }

        // Redis недоступний: версія живе лише в цьому екземплярі, тож списки в L1 все одно інвалідуються
        return $"local{_localVersions.GetValueOrDefault(versionKey)}";
    }

    public async Task BumpVersionAsync(string versionKey, string reason)
    {
        _localVersions.AddOrUpdate(versionKey, 1, (_, current) => current + 1);
        metrics.Invalidated(reason);

        if (await TryBumpL2Async(versionKey))
            logger.LogInformation("Cache version bumped {CacheKey}, reason {Reason}", versionKey, reason);
    }

    private void SetL1<T>(string key, T value, CacheProfile profile)
    {
        var options = new MemoryCacheEntryOptions
        {
            Size = profile.Size,
            SlidingExpiration = profile.L1Sliding,
            AbsoluteExpirationRelativeToNow = profile.L1Absolute,
            Priority = profile.Priority
        };
        options.RegisterPostEvictionCallback(static (_, _, reason, state) =>
        {
            if (reason is EvictionReason.Capacity or EvictionReason.Expired or EvictionReason.TokenExpired)
                ((CacheMetrics)state!).Evicted(reason.ToString());
        }, metrics);

        memory.Set(key, value, options);
    }

    private bool TryDeserialize<T>(string key, byte[] bytes, out T? value)
    {
        try
        {
            value = JsonSerializer.Deserialize<T>(bytes, Json);
            return value is not null;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Cache entry {CacheKey} is corrupted and will be reloaded", key);
            value = default;
            return false;
        }
    }

    private async Task<byte[]?> ReadL2Async(string key, CancellationToken ct)
    {
        if (!IsL2Available)
            return null;

        var sw = Stopwatch.StartNew();
        try
        {
            await FlushPendingAsync();
            var bytes = await distributed.GetAsync(key, ct);
            metrics.Duration(L2, sw.Elapsed.TotalMilliseconds);
            return bytes;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            MarkL2Unavailable(ex);
            return null;
        }
    }

    private async Task WriteL2Async(string key, byte[] bytes, TimeSpan ttl, CancellationToken ct)
    {
        if (!IsL2Available)
            return;

        try
        {
            await distributed.SetAsync(key, bytes,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            MarkL2Unavailable(ex);
        }
    }

    private async Task<bool> TryRemoveL2Async(string key)
    {
        try
        {
            await distributed.RemoveAsync(key, CancellationToken.None);
            return true;
        }
        catch (Exception ex)
        {
            _pendingRemovals[key] = 0;
            MarkL2Unavailable(ex);
            logger.LogWarning("Cache invalidation of {CacheKey} postponed until Redis is available", key);
            return false;
        }
    }

    private async Task<bool> TryBumpL2Async(string versionKey)
    {
        try
        {
            await distributed.SetStringAsync(versionKey, DateTime.UtcNow.Ticks.ToString(),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = VersionTtl }, CancellationToken.None);
            return true;
        }
        catch (Exception ex)
        {
            _pendingBumps[versionKey] = 0;
            MarkL2Unavailable(ex);
            logger.LogWarning("Cache version bump of {CacheKey} postponed until Redis is available", versionKey);
            return false;
        }
    }

    private async Task FlushPendingAsync()
    {
        foreach (var key in _pendingRemovals.Keys)
        {
            await distributed.RemoveAsync(key, CancellationToken.None);
            _pendingRemovals.TryRemove(key, out _);
            logger.LogInformation("Postponed cache invalidation applied {CacheKey}", key);
        }

        foreach (var key in _pendingBumps.Keys)
        {
            await distributed.SetStringAsync(key, DateTime.UtcNow.Ticks.ToString(),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = VersionTtl }, CancellationToken.None);
            _pendingBumps.TryRemove(key, out _);
            logger.LogInformation("Postponed cache version bump applied {CacheKey}", key);
        }
    }

    private void MarkL2Unavailable(Exception ex)
    {
        var retryAt = Environment.TickCount64 + (long)L2RetryDelay.TotalMilliseconds;
        var wasAvailable = IsL2Available;
        Interlocked.Exchange(ref _l2RetryAt, retryAt);

        if (wasAvailable)
            logger.LogWarning(ex, "Redis is unavailable, L2 is skipped for {RetryDelaySeconds} s", L2RetryDelay.TotalSeconds);
    }
}
