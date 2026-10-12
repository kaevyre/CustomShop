using Orders.Bll.Dtos;

namespace Orders.Bll.Services;

public interface ICustomerService
{
    Task<CustomerDto> GetAsync(long id, CancellationToken ct = default);
    Task<PagedResultDto<CustomerDto>> ListAsync(int page, int pageSize, CancellationToken ct = default);
    Task<CustomerDto> CreateAsync(CreateCustomerDto dto, CancellationToken ct = default);
    Task<CustomerDto> UpdateAsync(long id, UpdateCustomerDto dto, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}
