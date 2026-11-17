using System.Globalization;
using Catalog.Bll.Dtos;
using Microsoft.Extensions.Caching.Memory;

namespace Catalog.Bll.Caching;

/// <summary>Передбачувана схема ключів кешу Каталогу: префікс catalog: ізолює їх від інших сервісів у спільному Redis.</summary>
public static class CatalogCacheKeys
{
    public const string CategoriesAll = "catalog:categories:all";
    public const string BrandsAll = "catalog:brands:all";

    /// <summary>Ключ-версія для всіх списків товарів: масова інвалідація без перебору ключів.</summary>
    public const string ProductListsVersion = "catalog:products:list:version";

    public static string Product(long id) => $"catalog:product:{id}";

    public static string Category(int id, string listsVersion) => $"catalog:category:{id}:v{listsVersion}";

    public static string ProductsList(string listsVersion, ProductQuery q, int page, int pageSize) =>
        string.Create(CultureInfo.InvariantCulture,
            $"catalog:products:list:v{listsVersion}:p{page}:s{pageSize}:search={q.Search?.Trim()}:brand={q.BrandId}:category={q.CategoryId}:type={q.ProductType}:min={q.MinPrice}:max={q.MaxPrice}:custom={q.IsCustomizable}:sort={q.SortBy}:{q.SortDir}");
}

/// <summary>TTL за типом даних: картка товару часто читається (sliding), довідники рідко змінюються (довший absolute).</summary>
public static class CatalogCacheProfiles
{
    public static readonly CacheProfile Product = new(
        L1Sliding: TimeSpan.FromSeconds(30),
        L1Absolute: TimeSpan.FromMinutes(1),
        L2Ttl: TimeSpan.FromMinutes(10));

    public static readonly CacheProfile Reference = new(
        L1Sliding: TimeSpan.FromMinutes(2),
        L1Absolute: TimeSpan.FromMinutes(5),
        L2Ttl: TimeSpan.FromHours(1),
        Size: 10,
        Priority: CacheItemPriority.High);

    public static CacheProfile List(int pageSize) => new(
        L1Sliding: TimeSpan.FromSeconds(15),
        L1Absolute: TimeSpan.FromSeconds(30),
        L2Ttl: TimeSpan.FromMinutes(5),
        Size: pageSize);
}
