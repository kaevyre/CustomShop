using System.Data;
using Dapper;
using Orders.Dal.Abstractions;
using Orders.Dal.Infrastructure;
using Orders.Domain.Models;

namespace Orders.Dal.Repositories;

/// <summary>
/// Dapper-репозиторій замовлень: multi-mapping (Order + OrderDetails + Customer одним JOIN-запитом)
/// та виклики збережених процедур usp_CreateOrder / usp_ChangeOrderStatus.
/// </summary>
public sealed class OrderRepository : BaseDapperRepository<Order>, IOrderRepository
{
    private const string SelectWithDetailsSql = """
        SELECT o.Id, o.CustomerId, o.Status, o.OrderDate, o.PaidAt, o.ShippedAt,
               o.CreatedAt, o.CreatedBy, o.UpdatedAt, o.UpdatedBy, o.IsDeleted, o.RowVersion,
               d.OrderId, d.RecipientName, d.Phone, d.City, d.ShippingAddress, d.DeliveryMethod,
               d.EngravingText, d.Comment, d.UpdatedAt,
               c.Id, c.Email, c.FullName, c.Phone
        FROM dbo.Orders o
        LEFT JOIN dbo.OrderDetails d ON d.OrderId = o.Id
        JOIN dbo.Customers c ON c.Id = o.CustomerId
        WHERE o.Id = @Id AND o.IsDeleted = 0
        """;

    public OrderRepository(IDbSession session) : base(session)
    {
    }

    protected override string TableName => "Orders";
    protected override IReadOnlyList<string> Columns => ["CustomerId", "Status", "OrderDate", "PaidAt", "ShippedAt"];
    protected override IReadOnlyList<string> ReadOnlyColumns =>
        ["CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy", "IsDeleted", "RowVersion"];
    protected override bool SupportsSoftDelete => true;

    public async Task<Order?> GetWithDetailsAsync(long id, CancellationToken ct = default)
    {
        var connection = await Session.GetConnectionAsync(ct);

        // Перевантаження multi-mapping у Dapper не приймає CommandDefinition, тому токен скасування тут не передається
        return await GuardAsync(async () =>
        {
            var rows = await connection.QueryAsync<Order, OrderDetails?, Customer, Order>(
                SelectWithDetailsSql,
                (order, details, customer) =>
                {
                    order.Details = details is { OrderId: not 0 } ? details : null;
                    order.Customer = customer;
                    return order;
                },
                new { Id = id },
                Session.Transaction,
                splitOn: "OrderId,Id");

            return rows.FirstOrDefault();
        });
    }

    public override Task<long> AddAsync(Order entity, CancellationToken ct = default) =>
        CreateWithDetailsAsync(entity, ct);

    public async Task<long> CreateWithDetailsAsync(Order order, CancellationToken ct = default)
    {
        var details = order.Details
            ?? throw new InvalidOperationException("Order.Details must be set to create an order.");

        var parameters = new DynamicParameters();
        parameters.Add("CustomerId", order.CustomerId, DbType.Int64);
        parameters.Add("RecipientName", details.RecipientName, DbType.String, size: 200);
        parameters.Add("Phone", details.Phone, DbType.String, size: 20);
        parameters.Add("City", details.City, DbType.String, size: 100);
        parameters.Add("ShippingAddress", details.ShippingAddress, DbType.String, size: 400);
        parameters.Add("DeliveryMethod", details.DeliveryMethod, DbType.String, size: 20);
        parameters.Add("EngravingText", details.EngravingText, DbType.String, size: 50);
        parameters.Add("Comment", details.Comment, DbType.String, size: 1000);
        parameters.Add("User", order.CreatedBy, DbType.String, size: 100);
        parameters.Add("OrderId", dbType: DbType.Int64, direction: ParameterDirection.Output);

        var connection = await Session.GetConnectionAsync(ct);
        await GuardAsync(() => connection.ExecuteAsync(
            Command("dbo.usp_CreateOrder", parameters, ct, CommandType.StoredProcedure)));

        order.Id = parameters.Get<long>("OrderId");
        details.OrderId = order.Id;
        return order.Id;
    }

    public async Task<OrderStatusResult> ChangeStatusAsync(
        long orderId, string newStatus, byte[]? expectedRowVersion, string? user, CancellationToken ct = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("OrderId", orderId, DbType.Int64);
        parameters.Add("NewStatus", newStatus, DbType.String, size: 30);
        parameters.Add("ExpectedRowVersion", expectedRowVersion, DbType.Binary, size: 8);
        parameters.Add("User", user, DbType.String, size: 100);

        var connection = await Session.GetConnectionAsync(ct);
        return await GuardAsync(() => connection.QuerySingleAsync<OrderStatusResult>(
            Command("dbo.usp_ChangeOrderStatus", parameters, ct, CommandType.StoredProcedure)));
    }
}
