using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.EventBus.Events;
using RabbitMQ.Client;

namespace Platform.EventBus;

/// <summary>
/// Публікує інтеграційні події в exchange. Publisher нічого не знає про споживачів: exchange сам розкладає
/// повідомлення по чергах за routing key. IConnection — один на застосунок (singleton), канал на кожну публікацію.
/// Канал із publisher confirmations: BasicPublishAsync завершується, лише коли брокер підтвердив прийняття
/// повідомлення — без цього миттєве закриття каналу могло б загубити подію.
/// </summary>
public sealed class RabbitMqEventPublisher(
    IConnection connection,
    ICorrelationIdProvider correlation,
    ILogger<RabbitMqEventPublisher> logger) : IEventPublisher
{
    private static readonly CreateChannelOptions ConfirmedChannel = new(
        publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true);

    public async Task PublishAsync<T>(T @event, string exchange, string routingKey, CancellationToken ct = default)
        where T : IntegrationEvent
    {
        var eventType = typeof(T).Name;
        var correlationId = @event.CorrelationId ?? correlation.Current;

        // Producer-span: від нього consumer у іншому сервісі продовжить trace
        using var activity = TracePropagation.Source.StartActivity($"{exchange} publish", ActivityKind.Producer);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.operation", "publish");
        activity?.SetTag("messaging.destination.name", exchange);
        activity?.SetTag("messaging.rabbitmq.destination.routing_key", routingKey);
        activity?.SetTag("messaging.message.id", @event.EventId.ToString());
        activity?.SetTag("messaging.message.type", eventType);
        if (correlationId is not null)
            activity?.SetTag(CorrelationIdProvider.ActivityTag, correlationId);

        await using var channel = await connection.CreateChannelAsync(ConfirmedChannel, ct);

        var props = new BasicProperties
        {
            Persistent = true,                                   // delivery-mode 2: повідомлення переживає перезапуск брокера
            ContentType = "application/json",
            MessageId = @event.EventId.ToString(),
            Type = eventType,
            Timestamp = new AmqpTimestamp(new DateTimeOffset(@event.OccurredAt).ToUnixTimeSeconds()),
            Headers = new Dictionary<string, object?> { ["x-schema-version"] = @event.SchemaVersion }
        };
        if (correlationId is not null)
            props.Headers[CorrelationIdProvider.MessageHeader] = correlationId;
        TracePropagation.Inject(props);                          // W3C traceparent у заголовки

        var body = JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType(), JsonDefaults.Options);
        await channel.BasicPublishAsync(exchange, routingKey, mandatory: false, props, body, ct);

        logger.LogInformation(
            "Published {EventType} {EventId} to {Exchange}/{RoutingKey} (CorrelationId {CorrelationId})",
            eventType, @event.EventId, exchange, routingKey, correlationId);
    }
}
