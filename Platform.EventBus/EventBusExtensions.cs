using Microsoft.Extensions.Logging;
using Platform.EventBus;
using System.Diagnostics.Metrics;

namespace Microsoft.Extensions.DependencyInjection;

public static class EventBusExtensions
{
    /// <summary>
    /// Реєструє publisher, провайдер CorrelationId й ініціалізацію топології. Підключення до брокера
    /// (IConnection, health check, телеметрія Aspire) реєструється окремо в Program.cs сервісу:
    /// <c>builder.AddRabbitMQClient("messaging")</c> — рядок підключення Aspire передає автоматично.
    /// </summary>
    public static IServiceCollection AddEventBus(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<ICorrelationIdProvider, CorrelationIdProvider>();
        services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
        services.AddHostedService<TopologyInitializer>();
        return services;
    }

    /// <summary>Реєструє споживача як hosted service; його обробники — Scoped (створюються в scope повідомлення).</summary>
    public static IServiceCollection AddEventConsumer<TConsumer>(this IServiceCollection services)
        where TConsumer : EventConsumerBase
        => services.AddHostedService<TConsumer>();

    /// <summary>Gauge глибини dead letter queue для споживача (див. <see cref="DeadLetterDepthMonitor"/>).</summary>
    public static IServiceCollection AddDeadLetterDepthMonitor(this IServiceCollection services, string consumerQueue)
        => services.AddHostedService(sp => new DeadLetterDepthMonitor(
            sp, sp.GetRequiredService<IMeterFactory>(), sp.GetRequiredService<ILogger<DeadLetterDepthMonitor>>(),
            [Topology.DeadLetterQueueOf(consumerQueue)]));
}
