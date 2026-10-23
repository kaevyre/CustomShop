using Ardalis.Specification;
using Catalog.Bll.Dtos;
using Catalog.Domain.Entities;

namespace Catalog.Bll.Specifications;

/// <summary>
/// Список товарів: фільтри, сортування лише за дозволеними полями і сторінка.
/// Не залежить від EF Core; репозиторій застосовує її через WithSpecification, і все виконується одним SQL-запитом.
/// </summary>
public sealed class ProductsFilterSpec : Specification<Product>
{
    public ProductsFilterSpec(ProductQuery q, int page, int pageSize)
    {
        Query.Include(p => p.Brand).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            Query.Where(p => p.Name.Contains(term) || p.Sku.Contains(term));
        }

        if (q.BrandId is { } brandId)
            Query.Where(p => p.BrandId == brandId);
        if (q.CategoryId is { } categoryId)
            Query.Where(p => p.ProductCategories.Any(pc => pc.CategoryId == categoryId));
        if (!string.IsNullOrWhiteSpace(q.ProductType))
        {
            var productType = q.ProductType;
            Query.Where(p => p.ProductType == productType);
        }
        if (q.MinPrice is { } minPrice)
            Query.Where(p => p.BasePrice >= minPrice);
        if (q.MaxPrice is { } maxPrice)
            Query.Where(p => p.BasePrice <= maxPrice);
        if (q.IsCustomizable is { } isCustomizable)
            Query.Where(p => p.IsCustomizable == isCustomizable);

        var descending = string.Equals(q.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        switch (q.SortBy.ToLowerInvariant())
        {
            case "price":
                if (descending) Query.OrderByDescending(p => p.BasePrice).ThenBy(p => p.Id);
                else Query.OrderBy(p => p.BasePrice).ThenBy(p => p.Id);
                break;
            case "stock":
                if (descending) Query.OrderByDescending(p => p.Stock).ThenBy(p => p.Id);
                else Query.OrderBy(p => p.Stock).ThenBy(p => p.Id);
                break;
            case "created":
                if (descending) Query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id);
                else Query.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id);
                break;
            default:
                if (descending) Query.OrderByDescending(p => p.Name).ThenBy(p => p.Id);
                else Query.OrderBy(p => p.Name).ThenBy(p => p.Id);
                break;
        }

        Query.Skip((page - 1) * pageSize).Take(pageSize);
    }
}
