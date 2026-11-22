namespace Orders.Dal.Abstractions;

/// <summary>
/// Таблиця оброблених подій (dbo.ProcessedEvents) — основа ідемпотентності consumer-а.
/// RabbitMQ гарантує доставку «щонайменше один раз», тому та сама подія може прийти повторно.
/// </summary>
public interface IProcessedEventRepository
{
    /// <summary>
    /// Фіксує EventId як оброблений у поточній транзакції Unit of Work. <c>false</c> — такий EventId уже є
    /// (порушення PRIMARY KEY, помилка 2627): подію оброблено раніше, повторювати бізнес-дію не потрібно.
    /// </summary>
    Task<bool> TryAddAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>Створює таблицю, якщо її ще немає (БД, розгорнуті до Лаб. 08, не мають dbo.ProcessedEvents). Повторний виклик безпечний.</summary>
    Task EnsureSchemaAsync(CancellationToken ct = default);
}
