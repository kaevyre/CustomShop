using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Domain.Entities;
using Reviews.Infrastructure.Persistence;

namespace Reviews.Infrastructure.Seeding;

/// <summary>
/// Створює індекси колекцій на старті. Повторне створення наявного індексу з тими самими ключами, іменем
/// й опціями — no-op, тому імена й опції збігаються з db/p3/collections.js (Лаб. 01).
/// </summary>
public sealed class IndexCreationService(MongoDbContext context, ILogger<IndexCreationService> logger)
{
    public async Task CreateIndexesAsync(CancellationToken ct)
    {
        var reviewKeys = Builders<Review>.IndexKeys;
        var reviewIndexes = await context.Reviews.Indexes.CreateManyAsync(
        [
            // сторінка товару: «відгуки товару X, нові першими»
            new CreateIndexModel<Review>(
                reviewKeys.Ascending(r => r.ProductId).Descending(r => r.CreatedAt),
                new CreateIndexOptions { Name = "ix_product_createdAt" }),
            // профіль: «усі відгуки автора»
            new CreateIndexModel<Review>(
                reviewKeys.Ascending("author.profileId").Descending(r => r.CreatedAt),
                new CreateIndexOptions { Name = "ix_author_createdAt" }),
            // один автор — один відгук на товар (DuplicateKey → 409)
            new CreateIndexModel<Review>(
                reviewKeys.Ascending(r => r.ProductId).Ascending("author.profileId"),
                new CreateIndexOptions { Name = "ux_product_author", Unique = true }),
            // повнотекстовий пошук; "none" — без стемінгу (української немає серед мов MongoDB)
            new CreateIndexModel<Review>(
                reviewKeys.Text("title").Text(r => r.Text),
                new CreateIndexOptions { Name = "tx_title_text", DefaultLanguage = "none" })
        ], ct);

        var buildKeys = Builders<Build>.IndexKeys;
        var buildIndexes = await context.Builds.Indexes.CreateManyAsync(
        [
            // «сетапи, де використано товар X» — multikey по масиву items
            new CreateIndexModel<Build>(buildKeys.Ascending("items.productId"),
                new CreateIndexOptions { Name = "ix_items_productId" }),
            // фільтр за тегом, найпопулярніші першими
            new CreateIndexModel<Build>(buildKeys.Ascending("tags").Descending(b => b.Likes),
                new CreateIndexOptions { Name = "ix_tags_likes" })
        ], ct);

        logger.LogInformation("MongoDB indexes ensured: reviews [{ReviewIndexes}], builds [{BuildIndexes}]",
            string.Join(", ", reviewIndexes), string.Join(", ", buildIndexes));
    }
}

/// <summary>
/// Ініціалізація БД під час старту застосунку: спершу індекси (унікальний індекс має існувати до вставок),
/// потім сідери. Недоступна MongoDB не валить застосунок: API стартує й відповідає 503, health check — Unhealthy.
/// </summary>
public sealed class MongoInitializationHostedService(
    IndexCreationService indexes,
    IEnumerable<IDataSeeder> seeders,
    Microsoft.Extensions.Options.IOptions<SeedingOptions> seeding,
    ILogger<MongoInitializationHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            await indexes.CreateIndexesAsync(ct);

            if (!seeding.Value.Enabled)
            {
                logger.LogInformation("MongoDB seeding is disabled (MongoDb:Seed=false)");
                return;
            }
            foreach (var seeder in seeders)
                await seeder.SeedAsync(ct);
        }
        catch (Exception ex) when (ex is MongoException or TimeoutException)
        {
            logger.LogError(ex, "MongoDB initialization failed; indexes and seed data will be applied on next start");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

public sealed class SeedingOptions
{
    public bool Enabled { get; set; } = true;
}
