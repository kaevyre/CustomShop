using Orders.Domain.Models;

namespace Orders.Dal.Abstractions;

/// <summary>
/// Знімок товару з події Каталогу. <see cref="Stock"/> задається лише в ProductCreated і використовується при вставці копії;
/// <see cref="OccurredAt"/> — час події в Каталозі (зберігається в Products.SyncedAt).
/// </summary>
public sealed record ProductSnapshot(
    long Id, string Sku, string Name, decimal Price, int? Stock, bool IsActive, DateTime OccurredAt);

public interface IProductRepository : IGenericRepository<Product>
{
    /// <summary>
    /// Створює або оновлює локальну копію товару за подією з Каталогу. Застосовується, лише якщо подія НЕ старіша за вже
    /// застосовану (SyncedAt &lt;= OccurredAt) — подія, що прийшла пізніше за новішу, не перезапише новіші дані.
    /// <c>true</c> — зміну застосовано; <c>false</c> — подію пропущено як застарілу.
    /// </summary>
    Task<bool> UpsertSnapshotAsync(ProductSnapshot snapshot, CancellationToken ct = default);

    Task<Product?> GetBySkuAsync(string sku, CancellationToken ct = default);

    Task<IReadOnlyList<Product>> GetByIdsAsync(IEnumerable<long> ids, CancellationToken ct = default);
}
