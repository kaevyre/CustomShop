using System.Data;
using Microsoft.Data.SqlClient;
using Orders.Dal.Abstractions;
using Orders.Dal.Infrastructure;
using Orders.Domain.Exceptions;
using Orders.Domain.Models;

namespace Orders.Dal.Repositories;

/// <summary>
/// Репозиторій на «чистому» ADO.NET: SqlCommand + SqlDataReader, ручне відображення стовпців у моделі.
/// Не успадковує ні <see cref="IGenericRepository{T}"/>, ні <see cref="BaseDapperRepository{T}"/>.
/// </summary>
public sealed class OrderSummaryRepository : IOrderSummaryRepository
{
    private const string StatusStatisticsSql = """
        SELECT Status,
               COUNT(*) AS OrdersCount,
               CAST(COALESCE(SUM(TotalAmount), 0) AS DECIMAL(18,2)) AS TotalAmount
        FROM dbo.vw_OrderSummary
        GROUP BY Status
        ORDER BY Status
        """;

    private readonly IDbSession _session;

    public OrderSummaryRepository(IDbSession session)
    {
        _session = session;
    }

    public async Task<PagedResult<OrderSummary>> GetByCustomerAsync(
        long customerId, string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var connection = await _session.GetConnectionAsync(ct);

        await using var command = connection.CreateCommand();
        command.Transaction = _session.Transaction;
        command.CommandText = "dbo.usp_GetCustomerOrders";
        command.CommandType = CommandType.StoredProcedure;

        command.Parameters.Add(new SqlParameter("@CustomerId", SqlDbType.BigInt) { Value = customerId });
        command.Parameters.Add(new SqlParameter("@Status", SqlDbType.NVarChar, 30) { Value = (object?)status ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@PageNumber", SqlDbType.Int) { Value = page });
        command.Parameters.Add(new SqlParameter("@PageSize", SqlDbType.Int) { Value = pageSize });

        var items = new List<OrderSummary>();
        var totalCount = 0;

        try
        {
            await using var reader = await command.ExecuteReaderAsync(ct);

            var ordinalOrderId = reader.GetOrdinal("OrderId");
            var ordinalStatus = reader.GetOrdinal("Status");
            var ordinalOrderDate = reader.GetOrdinal("OrderDate");
            var ordinalPaidAt = reader.GetOrdinal("PaidAt");
            var ordinalShippedAt = reader.GetOrdinal("ShippedAt");
            var ordinalCity = reader.GetOrdinal("City");
            var ordinalDelivery = reader.GetOrdinal("DeliveryMethod");
            var ordinalItemsCount = reader.GetOrdinal("ItemsCount");
            var ordinalUnits = reader.GetOrdinal("UnitsTotal");
            var ordinalTotal = reader.GetOrdinal("TotalAmount");
            var ordinalTotalCount = reader.GetOrdinal("TotalCount");

            while (await reader.ReadAsync(ct))
            {
                items.Add(new OrderSummary
                {
                    OrderId = reader.GetInt64(ordinalOrderId),
                    Status = reader.GetString(ordinalStatus),
                    OrderDate = reader.GetDateTime(ordinalOrderDate),
                    PaidAt = reader.IsDBNull(ordinalPaidAt) ? null : reader.GetDateTime(ordinalPaidAt),
                    ShippedAt = reader.IsDBNull(ordinalShippedAt) ? null : reader.GetDateTime(ordinalShippedAt),
                    City = reader.IsDBNull(ordinalCity) ? null : reader.GetString(ordinalCity),
                    DeliveryMethod = reader.IsDBNull(ordinalDelivery) ? null : reader.GetString(ordinalDelivery),
                    ItemsCount = reader.GetInt32(ordinalItemsCount),
                    UnitsTotal = reader.GetInt32(ordinalUnits),
                    TotalAmount = reader.GetDecimal(ordinalTotal)
                });

                totalCount = reader.GetInt32(ordinalTotalCount);
            }
        }
        catch (SqlException ex) when (SqlExceptionTranslator.TryTranslate(ex, out var domain))
        {
            throw domain!;
        }

        return new PagedResult<OrderSummary>(items, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyList<OrderStatusStatistics>> GetStatusStatisticsAsync(CancellationToken ct = default)
    {
        var connection = await _session.GetConnectionAsync(ct);

        await using var command = connection.CreateCommand();
        command.Transaction = _session.Transaction;
        command.CommandText = StatusStatisticsSql;
        command.CommandType = CommandType.Text;

        var result = new List<OrderStatusStatistics>();

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new OrderStatusStatistics
            {
                Status = reader.GetString(0),
                OrdersCount = reader.GetInt32(1),
                TotalAmount = reader.GetDecimal(2)
            });
        }

        return result;
    }
}
