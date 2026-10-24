using Ardalis.Specification;
using Catalog.Domain.Entities;

namespace Catalog.Bll.Specifications;

/// <summary>Друга специфікація з іншими критеріями — працює через той самий WithSpecification без змін у репозиторії.</summary>
public sealed class ProductsByBrandSpec : Specification<Product>
{
    public ProductsByBrandSpec(int brandId, int page, int pageSize)
    {
        Query.Where(p => p.BrandId == brandId && p.IsActive)
             .Include(p => p.Brand)
             .AsNoTracking();

        Query.OrderBy(p => p.ProductType).ThenBy(p => p.BasePrice).ThenBy(p => p.Id);
        Query.Skip((page - 1) * pageSize).Take(pageSize);
    }
}
