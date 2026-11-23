using MongoDB.Bson;
using MongoDB.Driver;
using Platform.EventBus;
using Platform.EventBus.Events;
using Reviews.Application.Caching;
using Reviews.Domain.Entities;
using Reviews.Infrastructure.Persistence;

namespace Reviews.WebApi.IntegrationEvents;

/// <summary>
/// ProductRenamedEvent → оновити денормалізовану назву productName в усіх відгуках про цей товар
/// (політика з Лаб. 01: «оновлюється подіями з Каталогу», аналог onProductRenamed у db/p3/queries.js).
///
/// Ідемпотентність. MongoDB у контейнері — одиночний вузол без багатодокументних транзакцій, тому «EventId + зміна в одній
/// транзакції» неможливі. Натомість: (1) $set — ідемпотентна операція за природою, повторне застосування тієї самої події
/// нічого не змінює; (2) таблиця оброблених подій (колекція processedEvents, _id = EventId) пропускає дублікати;
/// (3) порядок «зміна → позначка» безпечний: збій між ними лише повторить ідемпотентну зміну.
/// Порядок подій. Колекція productSync зберігає час останньої застосованої події товару: подія, що прийшла пізніше за новішу
/// (out-of-order), не перезапише новішу назву. Назва в build-сетапах (builds.items[].productName) — знімок на момент
/// публікації, за політикою Лаб. 01 не оновлюється.
/// </summary>
public sealed class ProductRenamedHandler(
    MongoDbContext db,
    ReviewsCacheInvalidator cache,
    ILogger<ProductRenamedHandler> logger) : IEventHandler<ProductRenamedEvent>
{
    public const string ProcessedEventsCollection = "processedEvents";
    public const string ProductSyncCollection = "productSync";

    private IMongoCollection<BsonDocument> Processed => db.Database.GetCollection<BsonDocument>(ProcessedEventsCollection);
    private IMongoCollection<BsonDocument> Sync => db.Database.GetCollection<BsonDocument>(ProductSyncCollection);

    public async Task HandleAsync(ProductRenamedEvent e, CancellationToken ct)
    {
        var eventKey = e.EventId.ToString();

        // 1) ідемпотентність: подію вже оброблено
        if (await Processed.Find(new BsonDocument("_id", eventKey)).AnyAsync(ct))
        {
            logger.LogInformation("Duplicate event {EventType} {EventId} skipped (product {ProductId}, CorrelationId {CorrelationId})",
                nameof(ProductRenamedEvent), e.EventId, e.ProductId, e.CorrelationId);
            return;
        }

        // 2) порядок: застосовуємо, лише якщо подія не старіша за вже застосовану
        if (!await TryAdvanceAsync(e, ct))
        {
            logger.LogWarning(
                "Stale event {EventType} {EventId} for product {ProductId} ignored: a newer rename was already applied (occurred {OccurredAt:O})",
                nameof(ProductRenamedEvent), e.EventId, e.ProductId, e.OccurredAt);
            await MarkProcessedAsync(eventKey, ct);
            return;
        }

        // 3) бізнес-дія: назва товару в усіх відгуках про нього
        var result = await db.Reviews.UpdateManyAsync(
            r => r.ProductId == e.ProductId,
            Builders<Review>.Update.Set(r => r.ProductName, e.NewName),
            cancellationToken: ct);

        // 4) позначка «оброблено»
        await MarkProcessedAsync(eventKey, ct);

        logger.LogInformation(
            "Product {ProductId} renamed from \"{OldName}\" to \"{NewName}\" in {Modified} of {Matched} review(s) (event {EventId}, CorrelationId {CorrelationId})",
            e.ProductId, e.OldName, e.NewName, result.ModifiedCount, result.MatchedCount, e.EventId, e.CorrelationId);

        try
        {
            await cache.ListsChangedAsync("ProductRenamedFromCatalog");    // кешовані списки містять productName
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache invalidation after event {EventId} failed", e.EventId);
        }
    }

    /// <summary>
    /// Атомарно «просуває» час останньої події товару. Фільтр {_id, occurredAt &lt;= t} + upsert: якщо запис є й новіший,
    /// фільтр не знаходить його, upsert намагається вставити той самий _id — DuplicateKey означає «подія застаріла».
    /// «&lt;=» (а не «&lt;») дозволяє повторно застосувати ту саму подію після збою між кроками 2 і 4.
    /// </summary>
    private async Task<bool> TryAdvanceAsync(ProductRenamedEvent e, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", e.ProductId)
                     & Builders<BsonDocument>.Filter.Lte("occurredAt", e.OccurredAt);
        var update = Builders<BsonDocument>.Update
            .Set("occurredAt", e.OccurredAt)
            .Set("name", e.NewName);

        try
        {
            await Sync.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, ct);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    private async Task MarkProcessedAsync(string eventKey, CancellationToken ct)
    {
        try
        {
            await Processed.InsertOneAsync(
                new BsonDocument { { "_id", eventKey }, { "processedAt", DateTime.UtcNow } }, cancellationToken: ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // паралельний екземпляр уже позначив цю подію
        }
    }
}
