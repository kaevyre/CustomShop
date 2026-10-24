using Catalog.Domain.Entities;

namespace Catalog.Dal.Interfaces;

public interface ICategoryRepository : IGenericRepository<Category>
{
    /// <summary>Explicit Loading: категорія — першим запитом, її товари — другим, лише коли вони потрібні.</summary>
    Task<Category?> GetWithProductsAsync(int id, CancellationToken ct = default);

    Task<bool> ExistsBySlugAsync(string slug, CancellationToken ct = default);
}
