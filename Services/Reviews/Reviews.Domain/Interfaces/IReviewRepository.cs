using MongoDB.Bson;
using Reviews.Domain.Entities;

namespace Reviews.Domain.Interfaces;

/// <summary>
/// Контракт запису агрегату Review; реалізація — в Infrastructure (MongoDB Driver).
/// UpdateAsync застосовується лише до документа з очікуваною версією, інакше — ConflictException.
/// </summary>
public interface IReviewRepository
{
    Task<Review?> GetByIdAsync(ObjectId id, CancellationToken ct = default);

    /// <summary>Чи залишав автор уже відгук на цей товар (правило «один автор — один відгук»).</summary>
    Task<bool> ExistsAsync(long productId, ObjectId authorProfileId, CancellationToken ct = default);

    Task AddAsync(Review review, CancellationToken ct = default);
    Task UpdateAsync(Review review, CancellationToken ct = default);
    Task<bool> DeleteAsync(ObjectId id, int? expectedVersion, CancellationToken ct = default);
}
