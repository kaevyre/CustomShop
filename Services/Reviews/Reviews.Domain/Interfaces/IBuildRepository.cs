using MongoDB.Bson;
using Reviews.Domain.Entities;

namespace Reviews.Domain.Interfaces;

/// <summary>Контракт запису агрегату Build; реалізація — в Infrastructure.</summary>
public interface IBuildRepository
{
    Task<Build?> GetByIdAsync(ObjectId id, CancellationToken ct = default);
    Task AddAsync(Build build, CancellationToken ct = default);
    Task UpdateAsync(Build build, CancellationToken ct = default);
    Task<bool> DeleteAsync(ObjectId id, int? expectedVersion, CancellationToken ct = default);
}
