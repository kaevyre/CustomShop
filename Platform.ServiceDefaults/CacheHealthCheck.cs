using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Статистика кешу в /health (Лаб. 06): розмір L1, попадання/промахи, hit ratio і доступність Redis.
/// Недоступний Redis — це Degraded, а не Unhealthy: сервіс працює далі через L1 і базу даних.
/// </summary>
public sealed class CacheHealthCheck(IMemoryCache memory, IDistributedCache distributed) : IHealthCheck
{
    private const string ProbeKey = "health:cache-probe";
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(1);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var stats = memory.GetCurrentStatistics();
        var total = (stats?.TotalHits ?? 0) + (stats?.TotalMisses ?? 0);
        var data = new Dictionary<string, object>
        {
            ["l1.entries"] = stats?.CurrentEntryCount ?? 0,
            ["l1.size"] = stats?.CurrentEstimatedSize ?? 0,
            ["l1.hits"] = stats?.TotalHits ?? 0,
            ["l1.misses"] = stats?.TotalMisses ?? 0,
            ["l1.hitRatio"] = total == 0 ? 0 : Math.Round((double)(stats?.TotalHits ?? 0) / total, 3)
        };

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ProbeTimeout);
            await distributed.GetAsync(ProbeKey, timeout.Token).WaitAsync(ProbeTimeout, cancellationToken);
            data["l2.available"] = true;
            return HealthCheckResult.Healthy("Cache is available", data);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            data["l2.available"] = false;
            return HealthCheckResult.Degraded("Distributed cache is unavailable, working without L2", ex, data);
        }
    }
}
