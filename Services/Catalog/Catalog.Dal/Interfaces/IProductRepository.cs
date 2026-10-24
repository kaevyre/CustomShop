using Catalog.Domain.Entities;

namespace Catalog.Dal.Interfaces;

public interface IProductRepository : IGenericRepository<Product>
{
    /// <summary>Eager Loading: товар з брендом, специфікацією, опціями й категоріями одним SQL-запитом.</summary>
    Task<Product?> GetWithDetailsAsync(long id, CancellationToken ct = default);

    Task<bool> ExistsBySkuAsync(string sku, CancellationToken ct = default);
    Task<bool> ExistsBySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>Оптимістична конкурентність: SaveChanges перевірить, що RowVersion у БД не змінився.</summary>
    void SetOriginalRowVersion(Product product, byte[] rowVersion);
}
