using Reviews.Application.Common.Models;
using Reviews.Application.Reviews;

namespace Reviews.Application.Common.Interfaces;

/// <summary>Фільтр списку відгуків; усі умови застосовуються в MongoDB ($match / $text).</summary>
public sealed record ReviewFilter(long? ProductId, string? AuthorProfileId, int? MinRating, string? Search);

/// <summary>
/// Read-side відгуків (CQRS): обробники запитів працюють через цей інтерфейс,
/// а реалізація в Infrastructure проєктує документи одразу в DTO.
/// </summary>
public interface IReviewQueries
{
    Task<ReviewDetailsDto?> GetByIdAsync(string id, CancellationToken ct);
    Task<PagedResult<ReviewDto>> GetListAsync(ReviewFilter filter, int page, int pageSize, CancellationToken ct);
    Task<CursorPage<ReviewDto>> GetFeedAsync(long? productId, string? afterId, int pageSize, CancellationToken ct);
    Task<ProductRatingDto?> GetProductRatingAsync(long productId, CancellationToken ct);
}

/// <summary>Агрегована статистика оцінок товару ($group).</summary>
public sealed record ProductRatingDto(long ProductId, int ReviewsCount, double AverageRating,
    IReadOnlyDictionary<int, int> Distribution);
