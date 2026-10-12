using Orders.Bll.Dtos;

namespace Orders.Bll.Services;

public interface IProductService
{
    Task<ProductDto> GetAsync(long id, CancellationToken ct = default);
    Task<PagedResultDto<ProductDto>> ListAsync(int page, int pageSize, CancellationToken ct = default);
    Task<ProductDto> CreateAsync(CreateProductDto dto, CancellationToken ct = default);
    Task<ProductDto> UpdateAsync(long id, UpdateProductDto dto, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}
