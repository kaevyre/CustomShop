using Orders.Bll.Dtos;

namespace Orders.Bll.Services;

public interface IOrderService
{
    Task<OrderDto> GetAsync(long id, CancellationToken ct = default);

    Task<PagedResultDto<OrderSummaryDto>> GetCustomerOrdersAsync(
        long customerId, string? status, int page, int pageSize, CancellationToken ct = default);

    Task<OrderDto> PlaceAsync(CreateOrderDto dto, CancellationToken ct = default);

    Task<OrderStatusResultDto> ChangeStatusAsync(long id, ChangeOrderStatusDto dto, CancellationToken ct = default);

    Task<IReadOnlyList<OrderStatusStatisticsDto>> GetStatusStatisticsAsync(CancellationToken ct = default);
}
