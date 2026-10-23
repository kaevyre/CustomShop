using Catalog.Bll.Dtos;

namespace Catalog.Bll.Services;

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetListAsync(ProductQuery query, CancellationToken ct = default);
    Task<ProductDetailsDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<ProductDetailsDto> CreateAsync(CreateProductDto dto, CancellationToken ct = default);
    Task<ProductDto> ChangePriceAsync(long id, ChangePriceDto dto, CancellationToken ct = default);
    Task<ProductDto> AddStockAsync(long id, StockChangeDto dto, CancellationToken ct = default);
    Task<ProductDto> RemoveStockAsync(long id, StockChangeDto dto, CancellationToken ct = default);
    Task AssignCategoryAsync(long id, AssignCategoryDto dto, CancellationToken ct = default);
    Task RemoveCategoryAsync(long id, int categoryId, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default);
    Task<CategoryWithProductsDto> GetWithProductsAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<CategoryProductDto>> GetRecentProductsAsync(
        int id, DateTime? since, bool featuredOnly, CancellationToken ct = default);
    Task<CategoryDto> CreateAsync(CreateCategoryDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public interface IBrandService
{
    Task<IReadOnlyList<BrandDto>> GetAllAsync(CancellationToken ct = default);
    Task<PagedResult<ProductDto>> GetProductsAsync(int brandId, PageRequest paging, CancellationToken ct = default);
}
