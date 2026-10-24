using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Domain.Common;
using Reviews.Domain.Exceptions;

namespace Reviews.Infrastructure.Persistence.Repositories;

/// <summary>
/// Спільна для агрегатів логіка запису з оптимістичною конкурентністю: документ замінюється
/// лише якщо в БД досі та версія, яку прочитав обробник; інакше — ConflictException (409).
/// </summary>
public abstract class MongoRepositoryBase<T>(IMongoCollection<T> collection) where T : BaseEntity
{
    protected IMongoCollection<T> Collection { get; } = collection;

    public async Task<T?> GetByIdAsync(ObjectId id, CancellationToken ct = default)
        => await Collection.Find(e => e.Id == id).FirstOrDefaultAsync(ct);

    public Task AddAsync(T entity, CancellationToken ct = default)
        => Collection.InsertOneAsync(entity, cancellationToken: ct);

    public async Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        var expectedVersion = entity.Version;
        entity.BumpVersion();

        var result = await Collection.ReplaceOneAsync(
            ById(entity.Id) & VersionIs(expectedVersion), entity, cancellationToken: ct);

        if (result.MatchedCount == 0)                         // хтось змінив або видалив документ раніше
        {
            if (!await Collection.Find(ById(entity.Id)).AnyAsync(ct))
                throw new NotFoundException(typeof(T).Name, entity.Id.ToString());
            throw new ConflictException(
                $"{typeof(T).Name} '{entity.Id}' was modified by another request (expected version {expectedVersion})");
        }
    }

    public async Task<bool> DeleteAsync(ObjectId id, int? expectedVersion, CancellationToken ct = default)
    {
        var filter = ById(id);
        if (expectedVersion is { } version)
            filter &= VersionIs(version);

        var result = await Collection.DeleteOneAsync(filter, ct);
        return result.DeletedCount > 0;
    }

    protected static FilterDefinition<T> ById(ObjectId id) => Builders<T>.Filter.Eq(e => e.Id, id);

    /// <summary>Версія 0 = також документи без поля version (створені скриптами Лаб. 01).</summary>
    protected static FilterDefinition<T> VersionIs(int version)
    {
        var filter = Builders<T>.Filter;
        return version == 0
            ? filter.Or(filter.Eq(e => e.Version, 0), filter.Exists(e => e.Version, false))
            : filter.Eq(e => e.Version, version);
    }
}
