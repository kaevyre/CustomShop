using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.EventBus.Events;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Platform.EventBus;

/// <summary>
/// Довготривалий споживач черги як BackgroundService. Нащадок задає чергу й маршрути «тип події → обробник»;
/// уся надійність — тут:
/// <list type="bullet">
/// <item>autoAck: false + BasicQos (prefetch) — повідомлення підтверджується лише після успішної обробки;</item>
/// <item>успіх → BasicAck; постійний збій (некоректний JSON, невідомий тип, <see cref="PermanentEventException"/>) →
/// BasicNack(requeue: false) → dead letter exchange → DLQ;</item>
/// <item>тимчасовий збій (БД недоступна, таймаут) → повтор із лічильником x-retry-count, після максимуму — DLQ;</item>
/// <item>контекст трасування відновлюється з заголовків: обробка стає дочірнім span-ом публікації;</item>
/// <item>завершення через stoppingToken: неопрацьовані повідомлення лишаються в черзі.</item>
/// </list>
/// RabbitMQ гарантує доставку «щонайменше один раз», тому обробники мають бути ідемпотентними (за EventId).
/// </summary>
public abstract class EventConsumerBase(IConnection connection, IServiceScopeFactory scopes, ILogger logger) : BackgroundService
{
    public const string RetryHeader = "x-retry-count";
    private const ushort PrefetchCount = 10;
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    private static readonly CreateChannelOptions ConfirmedChannel = new(
        publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true);

    private IChannel? _channel;
    private CancellationToken _stoppingToken;

    /// <summary>Фабрика scope-ів для підготовчих кроків нащадка (<see cref="OnStartingAsync"/>).</summary>
    protected IServiceScopeFactory Scopes => scopes;

    /// <summary>Черга цього споживача (оголошується в <see cref="Topology.DeclareAsync"/>).</summary>
    protected abstract string QueueName { get; }

    /// <summary>Тип події (ім'я record-а, BasicProperties.Type) → десеріалізація й виклик обробника.</summary>
    protected abstract IReadOnlyDictionary<string, EventRoute> Routes { get; }

    protected virtual int MaxRetries => 3;

    /// <summary>Затримка перед повторною спробою: 1 с, 2 с, 4 с…</summary>
    protected virtual TimeSpan RetryDelay(int attempt) => TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));

    /// <summary>Підготовка перед стартом споживання (напр., створення таблиці оброблених подій).</summary>
    protected virtual Task OnStartingAsync(CancellationToken ct) => Task.CompletedTask;

    public delegate Task EventRoute(IServiceProvider services, ReadOnlyMemory<byte> body, CancellationToken ct);

    /// <summary>Маршрут для пари «подія → обробник»: десеріалізує тіло й викликає обробник зі scope повідомлення.</summary>
    protected static KeyValuePair<string, EventRoute> Route<TEvent, THandler>()
        where TEvent : IntegrationEvent
        where THandler : IEventHandler<TEvent>
        => new(typeof(TEvent).Name, async (services, body, ct) =>
        {
            var @event = JsonSerializer.Deserialize<TEvent>(body.Span, JsonDefaults.Options)
                         ?? throw new JsonException("Empty message");
            await services.GetRequiredService<THandler>().HandleAsync(@event, ct);
        });

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;

        // брокер може бути ще недоступний (старт у різному порядку) — не валимо застосунок, а пробуємо знову
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await OnStartingAsync(stoppingToken);

                _channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                await Topology.DeclareAsync(_channel, stoppingToken);                       // Частина 1.3
                await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: PrefetchCount, global: false, stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.ReceivedAsync += OnMessageAsync;
                await _channel.BasicConsumeAsync(QueueName, autoAck: false, consumer, stoppingToken);

                logger.LogInformation("Consumer started: queue {Queue}, prefetch {Prefetch}, handlers {Handlers}",
                    QueueName, PrefetchCount, string.Join(", ", Routes.Keys));
                break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Consumer of queue {Queue} could not start; retry in {Delay} s",
                    QueueName, ReconnectDelay.TotalSeconds);
                await DisposeChannelAsync();
                try { await Task.Delay(ReconnectDelay, stoppingToken); }
                catch (OperationCanceledException) { return; }
            }
        }

        // тримаємо сервіс «живим» до зупинки застосунку, тоді коректно закриваємо канал
        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }
        finally { await DisposeChannelAsync(); }
    }

    private async Task DisposeChannelAsync()
    {
        if (_channel is null)
            return;
        try { await _channel.DisposeAsync(); }
        catch (Exception ex) { logger.LogDebug(ex, "Channel of queue {Queue} was already closed", QueueName); }
        _channel = null;
    }

    // ---- Частини 3.2–3.4: розбір повідомлення, обробка, підтвердження ----

    private async Task OnMessageAsync(object sender, BasicDeliverEventArgs ea)
    {
        using var activity = TracePropagation.StartConsumerActivity(ea, QueueName);

        var headers = ea.BasicProperties.Headers;
        var messageId = ea.BasicProperties.MessageId;
        var eventType = ea.BasicProperties.Type;
        var correlationId = TracePropagation.ReadHeader(headers, CorrelationIdProvider.MessageHeader);

        // усі логи обробки (і обробників) отримують бізнес-контекст; TraceId додається з поточного Activity
        using var logScope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["EventId"] = messageId,
            ["EventType"] = eventType,
            ["CorrelationId"] = correlationId,
            ["Queue"] = QueueName
        });

        try
        {
            logger.LogInformation("Received {EventType} {EventId} from {Queue} (delivery {DeliveryTag}, redelivered {Redelivered})",
                eventType, messageId, QueueName, ea.DeliveryTag, ea.Redelivered);

            if (eventType is null || !Routes.TryGetValue(eventType, out var route))
                throw new PermanentEventException($"No handler for event type '{eventType}'");

            await using var scope = scopes.CreateAsyncScope();
            await route(scope.ServiceProvider, ea.Body, _stoppingToken);

            await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false, _stoppingToken);
            logger.LogInformation("Acked {EventType} {EventId}", eventType, messageId);
        }
        catch (OperationCanceledException) when (_stoppingToken.IsCancellationRequested)
        {
            // застосунок зупиняється: без ack повідомлення повернеться в чергу й буде оброблено після старту
            logger.LogWarning("Processing of {EventType} {EventId} interrupted by shutdown; message stays in the queue",
                eventType, messageId);
        }
        catch (Exception ex) when (ex is JsonException or PermanentEventException)
        {
            // постійний збій: повторення не допоможе → без requeue, RabbitMQ перенесе повідомлення в DLX → DLQ
            logger.LogError(ex, "Permanent failure for {EventType} {EventId}, sending to DLQ (reason: {Reason})",
                eventType, messageId, ex.Message);
            await TryNackAsync(ea);
        }
        catch (Exception ex)
        {
            await RetryOrDeadLetterAsync(ea, ex);                                          // Частина 5.1
        }
    }

    private async Task TryNackAsync(BasicDeliverEventArgs ea)
    {
        try
        {
            await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, _stoppingToken);
        }
        catch (Exception ex)
        {
            // канал закрито: невідправлений nack означає, що брокер поверне повідомлення в чергу
            logger.LogError(ex, "Could not nack message {MessageId}; broker will redeliver it", ea.BasicProperties.MessageId);
        }
    }

    // ---- Частина 5.1: повтори з лічильником ----

    private async Task RetryOrDeadLetterAsync(BasicDeliverEventArgs ea, Exception ex)
    {
        var messageId = ea.BasicProperties.MessageId;
        var headers = ea.BasicProperties.Headers ?? new Dictionary<string, object?>();
        var attempt = headers.TryGetValue(RetryHeader, out var v) && v is not null ? Convert.ToInt32(v) : 0;

        if (attempt >= MaxRetries)
        {
            logger.LogError(ex, "Message {MessageId} failed after {Attempts} attempts, sending to DLQ", messageId, attempt);
            await TryNackAsync(ea);                                                         // → DLX → DLQ
            return;
        }

        logger.LogWarning(ex, "Transient failure of {EventType} {EventId}; retry {Attempt}/{Max} in {Delay} s",
            ea.BasicProperties.Type, messageId, attempt + 1, MaxRetries, RetryDelay(attempt + 1).TotalSeconds);

        try
        {
            await Task.Delay(RetryDelay(attempt + 1), _stoppingToken);

            // Повернути повідомлення nack-ом із requeue не можна: воно прийде без змін, а заголовок змінити неможливо.
            // Тому копія з більшим лічильником публікується повторно, а оригінал підтверджується.
            var props = new BasicProperties(ea.BasicProperties)
            {
                Persistent = true,
                Headers = new Dictionary<string, object?>(headers) { [RetryHeader] = attempt + 1 }
            };

            // у ТУ САМУ чергу (default exchange + routing key = ім'я черги): публікація в catalog-events розіслала б копію
            // всім споживачам. Канал із confirms: оригінал підтверджуємо лише коли копію прийнято брокером.
            await using (var publisher = await connection.CreateChannelAsync(ConfirmedChannel, _stoppingToken))
                await publisher.BasicPublishAsync(exchange: string.Empty, routingKey: QueueName, mandatory: false,
                    basicProperties: props, body: ea.Body, cancellationToken: _stoppingToken);

            await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false, _stoppingToken);
        }
        catch (OperationCanceledException) when (_stoppingToken.IsCancellationRequested)
        {
            // оригінал не підтверджено — після старту повернеться в чергу
        }
        catch (Exception publishEx)
        {
            logger.LogError(publishEx, "Could not republish {MessageId} for retry; sending to DLQ", messageId);
            await TryNackAsync(ea);
        }
    }
}
