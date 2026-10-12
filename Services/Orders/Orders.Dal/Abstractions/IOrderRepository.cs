using Orders.Domain.Models;

namespace Orders.Dal.Abstractions;

public interface IOrderRepository : IGenericRepository<Order>
{
    /// <summary>Замовлення з деталями доставки та покупцем (multi-mapping). Позиції завантажує <see cref="IOrderItemRepository"/>.</summary>
    Task<Order?> GetWithDetailsAsync(long id, CancellationToken ct = default);

    Task<long> CreateWithDetailsAsync(Order order, CancellationToken ct = default);

    Task<OrderStatusResult> ChangeStatusAsync(
        long orderId, string newStatus, byte[]? expectedRowVersion, string? user, CancellationToken ct = default);
}
