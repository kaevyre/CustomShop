using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

public class ProductCategoryRepository : GenericRepository<ProductCategory>, IProductCategoryRepository
{
    public ProductCategoryRepository(CatalogDbContext context)
        : base(context)
    {
    }

    // M:N через проміжну сутність: фільтр і сортування за її власними полями AddedAt та IsFeatured
    public async Task<IReadOnlyList<ProductCategory>> GetRecentlyAddedAsync(
        int categoryId, DateTime since, bool featuredOnly, CancellationToken ct = default) =>
        await Set
            .Where(pc => pc.CategoryId == categoryId && pc.AddedAt >= since)
            .Where(pc => !featuredOnly || pc.IsFeatured)
            .Where(pc => pc.Product.IsActive)
            .OrderByDescending(pc => pc.IsFeatured)
            .ThenByDescending(pc => pc.AddedAt)
            .Include(pc => pc.Product).ThenInclude(p => p.Brand)
            .AsNoTracking()
            .ToListAsync(ct);
}
