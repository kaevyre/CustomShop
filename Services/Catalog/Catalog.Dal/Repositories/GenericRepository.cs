using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using Catalog.Dal.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly CatalogDbContext Context;
    protected readonly DbSet<T> Set;

    public GenericRepository(CatalogDbContext context)
    {
        Context = context;
        Set = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(object id, CancellationToken ct = default) =>
        await Set.FindAsync([id], ct);

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default) =>
        await Set.AsNoTracking().ToListAsync(ct);

    // Репозиторій лише інтерпретує специфікацію; ті самі методи працюють з будь-якою специфікацією
    public async Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification, CancellationToken ct = default) =>
        await Set.WithSpecification(specification).ToListAsync(ct);

    public Task<int> CountAsync(ISpecification<T> specification, CancellationToken ct = default) =>
        SpecificationEvaluator.Default
            .GetQuery(Set.AsQueryable(), specification, evaluateCriteriaOnly: true)
            .CountAsync(ct);

    public async Task AddAsync(T entity, CancellationToken ct = default) =>
        await Set.AddAsync(entity, ct);

    public void Update(T entity) => Set.Update(entity);

    public void Delete(T entity) => Set.Remove(entity);
}
