using Platform.EventBus.Events;

namespace Platform.EventBus;

public interface IEventPublisher
{
    /// <summary>Серіалізує подію в JSON і публікує її в exchange з routing key (persistent-доставка).</summary>
    Task PublishAsync<T>(T @event, string exchange, string routingKey, CancellationToken ct = default)
        where T : IntegrationEvent;
}

/// <summary>Обробник одного типу події. Створюється в scope повідомлення, тож може залежати від Scoped-сервісів (UoW, DbContext).</summary>
public interface IEventHandler<in TEvent> where TEvent : IntegrationEvent
{
    Task HandleAsync(TEvent @event, CancellationToken ct);
}

/// <summary>
/// Постійний збій обробки: повтор нічого не змінить (некоректні дані, порушення бізнес-правила).
/// Повідомлення одразу йде в dead letter queue, а не повторюється.
/// </summary>
public sealed class PermanentEventException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Звідки взяти CorrelationId поточної операції (HTTP-запит → заголовок X-Correlation-Id).</summary>
public interface ICorrelationIdProvider
{
    string? Current { get; }
}
