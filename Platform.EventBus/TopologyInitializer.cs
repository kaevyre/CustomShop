using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Platform.EventBus;

/// <summary>
/// Оголошує топологію під час старту застосунку. Потрібна й сервісу-publisher: якщо Каталог стартує раніше за споживачів,
/// черги з bindings уже існують, і подія не «зникає» через відсутність черги. Повторне оголошення безпечне.
/// Недоступний брокер не валить застосунок — спроби повторюються.
/// </summary>
public sealed class TopologyInitializer(IServiceProvider services, ILogger<TopologyInitializer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var connection = services.GetRequiredService<IConnection>();
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                await Topology.DeclareAsync(channel, stoppingToken);
                logger.LogInformation("RabbitMQ topology declared: exchanges {Exchange}, {Dlx}; queues {Orders}, {Reviews} with DLQs",
                    Topology.CatalogExchange, Topology.CatalogDeadLetterExchange,
                    Topology.OrdersCatalogSyncQueue, Topology.ReviewsCatalogSyncQueue);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not declare RabbitMQ topology; retry in 5 s");
                try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
                catch (OperationCanceledException) { return; }
            }
        }
    }
}
