using Orders.Domain.Models;

namespace Orders.Dal.Abstractions;

public interface IOrderItemRepository : IGenericRepository<OrderItem>
{
    /// <summary>Позиції замовлення разом з обраними опціями (multi-mapping «один-до-багатьох»).</summary>
    Task<IReadOnlyList<OrderItem>> GetByOrderIdAsync(long orderId, CancellationToken ct = default);

    /// <summary>Додає позицію та її опції (знімок), повертає Id позиції.</summary>
    Task<long> AddWithOptionsAsync(OrderItem item, CancellationToken ct = default);
}
