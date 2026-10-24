using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

public class ProductRepository : GenericRepository<Product>, IProductRepository
{
    public ProductRepository(CatalogDbContext context)
        : base(context)
    {
    }

    // Eager Loading: усе пов'язане — одним запитом із JOIN
    public Task<Product?> GetWithDetailsAsync(long id, CancellationToken ct = default) =>
        Set.Include(p => p.Brand)
           .Include(p => p.Specification)
           .Include(p => p.Options)
           .Include(p => p.ProductCategories).ThenInclude(pc => pc.Category)
           .FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<bool> ExistsBySkuAsync(string sku, CancellationToken ct = default) =>
        Set.IgnoreQueryFilters().AnyAsync(p => p.Sku == sku, ct);

    public Task<bool> ExistsBySlugAsync(string slug, CancellationToken ct = default) =>
        Set.IgnoreQueryFilters().AnyAsync(p => p.Slug == slug, ct);

    public void SetOriginalRowVersion(Product product, byte[] rowVersion) =>
        Context.Entry(product).Property(p => p.RowVersion).OriginalValue = rowVersion;
}
