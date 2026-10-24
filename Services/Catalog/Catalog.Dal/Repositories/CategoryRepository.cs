using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

public class CategoryRepository : GenericRepository<Category>, ICategoryRepository
{
    public CategoryRepository(CatalogDbContext context)
        : base(context)
    {
    }

    // Explicit Loading: два окремі запити
    public async Task<Category?> GetWithProductsAsync(int id, CancellationToken ct = default)
    {
        var category = await Set.FirstOrDefaultAsync(c => c.Id == id, ct);            // 1-й запит
        if (category is null)
            return null;

        await Context.Entry(category)
            .Collection(c => c.ProductCategories)
            .Query()
            .Include(pc => pc.Product)
            .OrderByDescending(pc => pc.AddedAt)
            .LoadAsync(ct);                                                             // 2-й запит

        return category;
    }

    public Task<bool> ExistsBySlugAsync(string slug, CancellationToken ct = default) =>
        Set.IgnoreQueryFilters().AnyAsync(c => c.Slug == slug, ct);
}
