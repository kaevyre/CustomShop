using Catalog.Dal.Interfaces;
using Catalog.Dal.Repositories;
using Catalog.Domain.Entities;
using Catalog.Domain.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal;

public class UnitOfWork : IUnitOfWork
{
    private readonly CatalogDbContext _context;

    public UnitOfWork(CatalogDbContext context)
    {
        _context = context;
        Products = new ProductRepository(context);
        Categories = new CategoryRepository(context);
        ProductCategories = new ProductCategoryRepository(context);
        Brands = new GenericRepository<Brand>(context);
    }

    public IProductRepository Products { get; }
    public ICategoryRepository Categories { get; }
    public IProductCategoryRepository ProductCategories { get; }
    public IGenericRepository<Brand> Brands { get; }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictException("The record was modified by another user. Reload it and try again.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException("A record with the same unique value already exists.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
        {
            throw new ConflictException("The operation violates a database constraint.", ex);
        }
    }
}
