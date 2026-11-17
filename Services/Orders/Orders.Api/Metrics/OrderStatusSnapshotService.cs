using Orders.Bll.Services;

namespace Orders.Api.Metrics;

/// <summary>
/// Періодично оновлює знімок для gauge orders.by_status. Дані беруться з GetStatusStatisticsAsync,
/// який уже кешується (Лаб. 06), тож це не створює додаткового навантаження на базу даних.
/// Callback gauge читає лише готовий знімок і не звертається до БД під час збирання метрик.
/// </summary>
public sealed class OrderStatusSnapshotService(
    IServiceScopeFactory scopes, OrderMetrics metrics, ILogger<OrderStatusSnapshotService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
                var statistics = await orders.GetStatusStatisticsAsync(stoppingToken);
                metrics.UpdateSnapshot(statistics.Select(s => new KeyValuePair<string, int>(s.Status, s.OrdersCount)));
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // метрики не мають валити сервіс: наступна спроба — через інтервал
                logger.LogWarning(ex, "Orders snapshot for orders.by_status was not refreshed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
