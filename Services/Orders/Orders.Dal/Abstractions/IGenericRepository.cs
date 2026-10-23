using Orders.Domain.Models;

namespace Orders.Dal.Abstractions;

public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(long id, CancellationToken ct = default);

    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);

    Task<PagedResult<T>> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);

    Task<long> AddAsync(T entity, CancellationToken ct = default);

    Task<bool> UpdateAsync(T entity, CancellationToken ct = default);

    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}
