using Orders.Bll.Caching;
using Orders.Dal.Abstractions;
using Platform.EventBus;
using Platform.EventBus.Events;

namespace Orders.Api.IntegrationEvents;

/// <summary>
/// Оновлення локальної копії довідника Products за подією з Каталогу (політика узгодженості з Лаб. 01:
/// «оновлюється подіями з Каталогу»; SyncedAt — час останньої застосованої події).
/// Усі події Каталогу несуть знімок товару, тож обробка однакова: ідемпотентний upsert за EventId.
/// Ціни вже оформлених замовлень (OrderItems.UnitPrice) не чіпаємо — це знімок на момент покупки.
/// Джерелом істини лишається Каталог; це не синхронізація баз, а оновлення копії для запитів Замовлень.
/// </summary>
public abstract class ProductSnapshotHandler<TEvent>(IUnitOfWork uow, ITwoLevelCache cache, ILogger logger)
    : IEventHandler<TEvent> where TEvent : IntegrationEvent
{
    protected abstract ProductSnapshot ToSnapshot(TEvent @event);

    public async Task HandleAsync(TEvent @event, CancellationToken ct)
    {
        var snapshot = ToSnapshot(@event);
        var applied = false;

        await uow.BeginAsync(ct: ct);
        try
        {
            // 1) ідемпотентність: EventId і бізнес-зміна — в ОДНІЙ транзакції (обидва збережуться або жоден)
            if (!await uow.ProcessedEvents.TryAddAsync(@event.EventId, ct))
            {
                logger.LogInformation(
                    "Duplicate event {EventType} {EventId} skipped (product {ProductId}, CorrelationId {CorrelationId})",
                    typeof(TEvent).Name, @event.EventId, snapshot.Id, @event.CorrelationId);
                await uow.RollbackAsync(CancellationToken.None);
                return;
            }

            // 2) бізнес-дія: оновити (або створити) копію товару, якщо подія не застаріла
            applied = await uow.Products.UpsertSnapshotAsync(snapshot, ct);
            await uow.CommitAsync(ct);
        }
        catch
        {
            await uow.RollbackAsync(CancellationToken.None);
            throw;                                    // consumer вирішить: повтор (тимчасовий збій) чи DLQ
        }

        if (applied)
        {
            logger.LogInformation(
                "Product {ProductId} synced from {EventType} {EventId}: {Sku}, \"{Name}\", price {Price}, active {IsActive} (CorrelationId {CorrelationId})",
                snapshot.Id, typeof(TEvent).Name, @event.EventId, snapshot.Sku, snapshot.Name, snapshot.Price,
                snapshot.IsActive, @event.CorrelationId);
            await InvalidateCacheAsync(snapshot.Id, @event.EventId);
        }
        else
        {
            logger.LogWarning(
                "Stale event {EventType} {EventId} for product {ProductId} ignored: a newer change was already applied (occurred {OccurredAt:O})",
                typeof(TEvent).Name, @event.EventId, snapshot.Id, snapshot.OccurredAt);
        }
    }

    /// <summary>Після коміту: картка товару й списки в кеші (Лаб. 06) більше не актуальні.</summary>
    private async Task InvalidateCacheAsync(long productId, Guid eventId)
    {
        try
        {
            await cache.InvalidateAsync("ProductSyncedFromCatalog", OrdersCacheKeys.Product(productId));
            await cache.BumpVersionAsync(OrdersCacheKeys.ProductListsVersion, "ProductSyncedFromCatalog");
        }
        catch (Exception ex)
        {
            // зміну вже збережено; повтор події дав би «дублікат», тому збій кешу лише логуємо
            logger.LogWarning(ex, "Cache invalidation after event {EventId} failed for product {ProductId}", eventId, productId);
        }
    }
}

public sealed class ProductCreatedHandler(IUnitOfWork uow, ITwoLevelCache cache, ILogger<ProductCreatedHandler> logger)
    : ProductSnapshotHandler<ProductCreatedEvent>(uow, cache, logger)
{
    protected override ProductSnapshot ToSnapshot(ProductCreatedEvent e) =>
        new(e.ProductId, e.Sku, e.ProductName, e.Price, e.Stock, e.IsActive, e.OccurredAt);
}

public sealed class ProductPriceChangedHandler(IUnitOfWork uow, ITwoLevelCache cache, ILogger<ProductPriceChangedHandler> logger)
    : ProductSnapshotHandler<ProductPriceChangedEvent>(uow, cache, logger)
{
    protected override ProductSnapshot ToSnapshot(ProductPriceChangedEvent e) =>
        new(e.ProductId, e.Sku, e.ProductName, e.NewPrice, Stock: null, e.IsActive, e.OccurredAt);
}

public sealed class ProductRenamedHandler(IUnitOfWork uow, ITwoLevelCache cache, ILogger<ProductRenamedHandler> logger)
    : ProductSnapshotHandler<ProductRenamedEvent>(uow, cache, logger)
{
    protected override ProductSnapshot ToSnapshot(ProductRenamedEvent e) =>
        new(e.ProductId, e.Sku, e.NewName, e.Price, Stock: null, e.IsActive, e.OccurredAt);
}

/// <summary>Товар видалено в Каталозі (м'яке видалення): копія лишається, бо на неї посилаються замовлення, але стає неактивною.</summary>
public sealed class ProductDeletedHandler(IUnitOfWork uow, ITwoLevelCache cache, ILogger<ProductDeletedHandler> logger)
    : ProductSnapshotHandler<ProductDeletedEvent>(uow, cache, logger)
{
    protected override ProductSnapshot ToSnapshot(ProductDeletedEvent e) =>
        new(e.ProductId, e.Sku, e.ProductName, e.Price, Stock: null, IsActive: false, e.OccurredAt);
}
