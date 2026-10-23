using Catalog.Domain.Entities;

namespace Catalog.Dal.Interfaces;

public interface IProductCategoryRepository : IGenericRepository<ProductCategory>
{
    /// <summary>
    /// LINQ to Entities через проміжну сутність M:N і її власні поля:
    /// товари категорії, додані після <paramref name="since"/>, рекомендовані першими.
    /// </summary>
    Task<IReadOnlyList<ProductCategory>> GetRecentlyAddedAsync(
        int categoryId, DateTime since, bool featuredOnly, CancellationToken ct = default);
}
