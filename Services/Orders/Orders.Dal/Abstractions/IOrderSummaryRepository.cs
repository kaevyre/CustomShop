using Orders.Domain.Models;

namespace Orders.Dal.Abstractions;

/// <summary>
/// Репозиторій лише для читання, написаний на «чистому» ADO.NET (SqlCommand / SqlDataReader).
/// Свідомо НЕ успадковує <see cref="IGenericRepository{T}"/>: це звіти, а не CRUD сутності.
/// </summary>
public interface IOrderSummaryRepository
{
    Task<PagedResult<OrderSummary>> GetByCustomerAsync(
        long customerId, string? status, int page, int pageSize, CancellationToken ct = default);

    Task<IReadOnlyList<OrderStatusStatistics>> GetStatusStatisticsAsync(CancellationToken ct = default);
}
