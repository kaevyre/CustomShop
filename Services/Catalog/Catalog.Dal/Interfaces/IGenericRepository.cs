using Ardalis.Specification;

namespace Catalog.Dal.Interfaces;

/// <summary>
/// Async Generic Repository на DbSet&lt;T&gt;. Методи, що звертаються до БД, асинхронні;
/// Update і Delete лише змінюють стан сутності в контексті. Зберігає зміни тільки Unit of Work.
/// </summary>
public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(object id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Вибірка за специфікацією: фільтр, Include, сортування, Skip/Take виконуються в SQL.</summary>
    Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification, CancellationToken ct = default);

    /// <summary>Кількість записів за критеріями специфікації (без Include, сортування й пагінації).</summary>
    Task<int> CountAsync(ISpecification<T> specification, CancellationToken ct = default);

    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Delete(T entity);
}
