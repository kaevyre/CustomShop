using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Platform.EventBus;

/// <summary>
/// Моніторинг глибини DLQ: gauge <c>messaging.dlq.depth</c> (тег queue) у Meter "Platform.Messaging" → вкладка Metrics
/// в Aspire Dashboard. Асинхронні збої «тихі» — немає клієнта, який одразу побачить помилку, тому зростання DLQ
/// має бути видно. Повідомлення в DLQ не відновлюються автоматично — їх розбирають вручну.
/// </summary>
public sealed class DeadLetterDepthMonitor : BackgroundService
{
    public const string MeterName = "Platform.Messaging";
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    private readonly IServiceProvider _services;
    private readonly ILogger<DeadLetterDepthMonitor> _logger;
    private readonly string[] _queues;
    private readonly Dictionary<string, long> _depth = new();
    private readonly object _sync = new();

    public DeadLetterDepthMonitor(IServiceProvider services, IMeterFactory meterFactory,
        ILogger<DeadLetterDepthMonitor> logger, string[] deadLetterQueues)
    {
        _services = services;
        _logger = logger;
        _queues = deadLetterQueues;

        var meter = meterFactory.Create(MeterName);
        meter.CreateObservableGauge("messaging.dlq.depth", Observe, unit: "{message}",
            description: "Messages waiting in dead letter queues (need manual investigation)");
    }

    private IEnumerable<Measurement<long>> Observe()
    {
        lock (_sync)
            return _depth.Select(kv => new Measurement<long>(kv.Value, new KeyValuePair<string, object?>("queue", kv.Key))).ToArray();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var queue in _queues)
            {
                try
                {
                    var connection = _services.GetRequiredService<IConnection>();
                    // окремий канал на кожну перевірку: passive-declare неіснуючої черги закриває канал помилкою 404
                    await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                    var ok = await channel.QueueDeclarePassiveAsync(queue, stoppingToken);
                    lock (_sync) _depth[queue] = ok.MessageCount;

                    if (ok.MessageCount > 0)
                        _logger.LogWarning("Dead letter queue {Queue} contains {Depth} message(s) — manual investigation required",
                            queue, ok.MessageCount);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not read depth of {Queue}", queue);
                }
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
