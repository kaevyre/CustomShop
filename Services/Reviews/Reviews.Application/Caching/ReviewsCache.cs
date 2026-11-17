using MediatR;

namespace Reviews.Application.Caching;

/// <summary>Ключі кешу сервісу Відгуків: префікс reviews: ізолює їх від інших сервісів у спільному Redis.</summary>
public static class ReviewsCacheKeys
{
    /// <summary>Ключ-версія для всіх списків відгуків.</summary>
    public const string ListsVersion = "reviews:list:version";

    public static string ProductRating(long productId) => $"reviews:product:{productId}:rating";

    public static string List(string version, long? productId, string? authorProfileId, int? minRating, string? search, int page, int pageSize) =>
        $"reviews:list:v{version}:product={productId}:author={authorProfileId}:min={minRating}:search={search}:p{page}:s{pageSize}";
}

public static class ReviewsCacheProfiles
{
    public static readonly CacheProfile Rating = new(
        L1Sliding: TimeSpan.FromSeconds(30),
        L1Absolute: TimeSpan.FromMinutes(1),
        L2Ttl: TimeSpan.FromMinutes(10));

    public static CacheProfile List(int pageSize) => new(
        L1Sliding: TimeSpan.FromSeconds(15),
        L1Absolute: TimeSpan.FromSeconds(30),
        L2Ttl: TimeSpan.FromMinutes(5),
        Size: pageSize);
}

/// <summary>Запит, результат якого кешує <see cref="CachingBehavior{TRequest,TResponse}"/>.</summary>
public interface ICacheableQuery
{
    /// <summary>true — ключ залежить від версії списків (масова інвалідація).</summary>
    bool UsesListsVersion => false;

    string GetCacheKey(string listsVersion);

    CacheProfile GetCacheProfile();
}

/// <summary>Cache-aside для CQRS-запитів: після валідації, до обробника; команди кеш не зачіпають.</summary>
public sealed class CachingBehavior<TRequest, TResponse>(ITwoLevelCache cache) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not ICacheableQuery query)
            return await next();

        var version = query.UsesListsVersion ? await cache.GetVersionAsync(ReviewsCacheKeys.ListsVersion, ct) : string.Empty;

        return (await cache.GetOrCreateAsync<TResponse>(
            query.GetCacheKey(version), query.GetCacheProfile(), async _ => await next(), ct))!;
    }
}

/// <summary>Інвалідація після запису: рейтинг товару і всі списки, у які входить відгук.</summary>
public sealed class ReviewsCacheInvalidator(ITwoLevelCache cache)
{
    public async Task ProductReviewsChangedAsync(long productId, string reason)
    {
        await cache.InvalidateAsync(reason, ReviewsCacheKeys.ProductRating(productId));
        await cache.BumpVersionAsync(ReviewsCacheKeys.ListsVersion, reason);
    }

    public Task ListsChangedAsync(string reason) => cache.BumpVersionAsync(ReviewsCacheKeys.ListsVersion, reason);
}
