using Orders.Dal.Abstractions;
using Platform.EventBus;
using Platform.EventBus.Events;
using RabbitMQ.Client;

namespace Orders.Api.IntegrationEvents;

/// <summary>
/// Споживач черги orders.catalog-sync (BackgroundService із ручним підтвердженням, див. <see cref="EventConsumerBase"/>):
/// події Каталогу → оновлення локальної копії Products. Кілька екземплярів orders-api читають одну чергу
/// (competing consumers) — повідомлення розподіляються між ними.
/// </summary>
public sealed class CatalogEventsConsumer(IConnection connection, IServiceScopeFactory scopes, ILogger<CatalogEventsConsumer> logger)
    : EventConsumerBase(connection, scopes, logger)
{
    protected override string QueueName => Topology.OrdersCatalogSyncQueue;

    protected override IReadOnlyDictionary<string, EventRoute> Routes { get; } = new[]
    {
        Route<ProductCreatedEvent, ProductCreatedHandler>(),
        Route<ProductPriceChangedEvent, ProductPriceChangedHandler>(),
        Route<ProductRenamedEvent, ProductRenamedHandler>(),
        Route<ProductDeletedEvent, ProductDeletedHandler>()
    }.ToDictionary(route => route.Key, route => route.Value);

    /// <summary>БД, розгорнуті до Лаб. 08, не мають таблиці ProcessedEvents — створюємо її (скрипт ідемпотентний).</summary>
    protected override async Task OnStartingAsync(CancellationToken ct)
    {
        await using var scope = Scopes.CreateAsyncScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await uow.ProcessedEvents.EnsureSchemaAsync(ct);
        logger.LogInformation("Table dbo.ProcessedEvents is ready");
    }
}

public static class CatalogSyncRegistration
{
    /// <summary>Обробники створюються в scope повідомлення (залежать від Scoped IUnitOfWork).</summary>
    public static IServiceCollection AddCatalogSyncHandlers(this IServiceCollection services) => services
        .AddScoped<ProductCreatedHandler>()
        .AddScoped<ProductPriceChangedHandler>()
        .AddScoped<ProductRenamedHandler>()
        .AddScoped<ProductDeletedHandler>();
}
