namespace Platform.EventBus.Events;

/// <summary>
/// Базова інтеграційна подія. Незмінний record: порівнюється за значенням, десеріалізується через конструктор.
/// <see cref="EventId"/> — ключ ідемпотентності (споживач зберігає оброблені EventId),
/// <see cref="CorrelationId"/> — бізнес-трасування (той самий X-Correlation-Id, що й у HTTP-запиті, який спричинив подію),
/// <see cref="SchemaVersion"/> — версія контракту: нові поля додаються, наявні не перейменовуються й не видаляються.
/// </summary>
public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string? CorrelationId { get; init; }
    public int SchemaVersion { get; init; } = 1;
}
