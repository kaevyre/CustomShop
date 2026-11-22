using MongoDB.Bson;
using MongoDB.Driver;
using Platform.EventBus;
using Platform.EventBus.Events;
using RabbitMQ.Client;
using Reviews.Infrastructure.Persistence;

namespace Reviews.WebApi.IntegrationEvents;

/// <summary>
/// Споживач черги reviews.catalog-sync: події Каталогу → оновлення productName у відгуках.
/// BackgroundService із ручним підтвердженням, повтори й DLQ — у <see cref="EventConsumerBase"/>.
/// </summary>
public sealed class CatalogEventsConsumer(IConnection connection, IServiceScopeFactory scopes, ILogger<CatalogEventsConsumer> logger)
    : EventConsumerBase(connection, scopes, logger)
{
    protected override string QueueName => Topology.ReviewsCatalogSyncQueue;

    protected override IReadOnlyDictionary<string, EventRoute> Routes { get; } = new[]
    {
        Route<ProductRenamedEvent, ProductRenamedHandler>()
    }.ToDictionary(route => route.Key, route => route.Value);

    /// <summary>Записи про оброблені події не накопичуються вічно: TTL-індекс прибирає їх через 30 днів.</summary>
    protected override async Task OnStartingAsync(CancellationToken ct)
    {
        await using var scope = Scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
        var processed = db.Database.GetCollection<BsonDocument>(ProductRenamedHandler.ProcessedEventsCollection);
        await processed.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("processedAt"),
                new CreateIndexOptions { Name = "ttl_processedAt", ExpireAfter = TimeSpan.FromDays(30) }),
            cancellationToken: ct);
        logger.LogInformation("Collection {Collection} is ready", ProductRenamedHandler.ProcessedEventsCollection);
    }
}

public static class CatalogSyncRegistration
{
    /// <summary>Обробники створюються в scope повідомлення.</summary>
    public static IServiceCollection AddCatalogSyncHandlers(this IServiceCollection services) =>
        services.AddScoped<ProductRenamedHandler>();
}
