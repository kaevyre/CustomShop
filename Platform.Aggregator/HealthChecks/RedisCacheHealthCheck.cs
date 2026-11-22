using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Platform.Aggregator.HealthChecks;

/// <summary>
/// Redis (кеш агрегованих карток): читання пробного ключа. Кеш необов'язковий — без нього картка збирається з сервісів,
/// тому збій дає Degraded, а не Unhealthy (graceful degradation, див. OrderCardCache).
/// </summary>
public sealed class RedisCacheHealthCheck(IDistributedCache cache) : IHealthCheck
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(1);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await cache.GetAsync("health:cache-probe", cancellationToken).WaitAsync(ProbeTimeout, cancellationToken);
            return HealthCheckResult.Healthy("Redis cache is available");
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            return new HealthCheckResult(context.Registration.FailureStatus,
                "Redis cache is unavailable, cards are built without cache", ex);
        }
    }
}
