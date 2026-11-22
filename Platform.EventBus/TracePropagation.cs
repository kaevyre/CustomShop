using System.Diagnostics;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Platform.EventBus;

/// <summary>
/// Передача контексту трасування через брокер у стандартному форматі W3C (заголовки traceparent / tracestate):
/// publisher записує контекст поточного Activity у заголовки повідомлення, consumer відновлює його й створює
/// дочірній span — так HTTP-запит → публікація → обробка → база даних утворюють один trace.
/// Реалізовано вручну (а не пакетом RabbitMQ.Client.OpenTelemetry), щоб було видно, як контекст переходить через брокер.
/// </summary>
public static class TracePropagation
{
    /// <summary>Назва ActivitySource; підключається в ServiceDefaults: <c>.AddSource("Platform.Messaging")</c>.</summary>
    public const string SourceName = "Platform.Messaging";

    public static readonly ActivitySource Source = new(SourceName);
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    /// <summary>Записує контекст поточного Activity (traceparent) у заголовки повідомлення.</summary>
    public static void Inject(BasicProperties props)
    {
        if (Activity.Current is not { } activity)
            return;

        props.Headers ??= new Dictionary<string, object?>();
        Propagator.Inject(new PropagationContext(activity.Context, Baggage.Current), props.Headers,
            (headers, key, value) => headers[key] = value);
    }

    /// <summary>Відновлює батьківський контекст із заголовків отриманого повідомлення й починає Consumer-span.</summary>
    public static Activity? StartConsumerActivity(BasicDeliverEventArgs ea, string queue)
    {
        var parent = Propagator.Extract(default, ea.BasicProperties.Headers,
            (headers, key) => headers is not null && headers.TryGetValue(key, out var v) && v is byte[] bytes
                ? new[] { Encoding.UTF8.GetString(bytes) }          // значення рядкових заголовків приходять як byte[]
                : Array.Empty<string>());

        var activity = Source.StartActivity($"{queue} process", ActivityKind.Consumer, parent.ActivityContext);
        if (activity is null)
            return null;

        activity.SetTag("messaging.system", "rabbitmq");
        activity.SetTag("messaging.operation", "process");
        activity.SetTag("messaging.destination.name", queue);
        activity.SetTag("messaging.message.id", ea.BasicProperties.MessageId);
        activity.SetTag("messaging.message.type", ea.BasicProperties.Type);
        activity.SetTag("messaging.rabbitmq.destination.routing_key", ea.RoutingKey);

        var correlationId = ReadHeader(ea.BasicProperties.Headers, CorrelationIdProvider.MessageHeader);
        if (correlationId is not null)
            activity.SetTag(CorrelationIdProvider.ActivityTag, correlationId);

        return activity;
    }

    /// <summary>Рядковий заголовок повідомлення (RabbitMQ повертає рядки як byte[]).</summary>
    public static string? ReadHeader(IDictionary<string, object?>? headers, string name) =>
        headers is not null && headers.TryGetValue(name, out var value)
            ? value switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                string s => s,
                _ => value?.ToString()
            }
            : null;
}
