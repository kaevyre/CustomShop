using Catalog.Domain.Entities;

namespace Catalog.Dal.Interfaces;

/// <summary>
/// Unit of Work: усі репозиторії працюють з одним CatalogDbContext. Репозиторії лише додають, змінюють
/// чи видаляють сутності в контексті; SaveChangesAsync один раз наприкінці бізнес-операції зберігає все
/// в одній транзакції.
/// </summary>
public interface IUnitOfWork
{
    IProductRepository Products { get; }
    ICategoryRepository Categories { get; }
    IProductCategoryRepository ProductCategories { get; }
    IGenericRepository<Brand> Brands { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
