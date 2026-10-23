using Dapper;
using Orders.Dal.Abstractions;
using Orders.Dal.Infrastructure;
using Orders.Domain.Models;

namespace Orders.Dal.Repositories;

/// <summary>Dapper-репозиторій позицій замовлення (multi-mapping «позиція → її опції»).</summary>
public sealed class OrderItemRepository : BaseDapperRepository<OrderItem>, IOrderItemRepository
{
    private const string SelectWithOptionsSql = """
        SELECT i.Id, i.OrderId, i.ProductId, i.ProductName, i.UnitPrice, i.Units, i.Discount, i.CreatedAt,
               op.Id, op.OrderItemId, op.OptionId, op.OptionGroup, op.OptionValue, op.PriceDelta
        FROM dbo.OrderItems i
        LEFT JOIN dbo.OrderItemOptions op ON op.OrderItemId = i.Id
        WHERE i.OrderId = @OrderId
        ORDER BY i.Id, op.Id
        """;

    private const string InsertOptionSql = """
        INSERT INTO dbo.OrderItemOptions (OrderItemId, OptionId, OptionGroup, OptionValue, PriceDelta)
        VALUES (@OrderItemId, @OptionId, @OptionGroup, @OptionValue, @PriceDelta)
        """;

    public OrderItemRepository(IDbSession session) : base(session)
    {
    }

    protected override string TableName => "OrderItems";
    protected override IReadOnlyList<string> Columns =>
        ["OrderId", "ProductId", "ProductName", "UnitPrice", "Units", "Discount"];
    protected override IReadOnlyList<string> ReadOnlyColumns => ["CreatedAt"];

    public async Task<IReadOnlyList<OrderItem>> GetByOrderIdAsync(long orderId, CancellationToken ct = default)
    {
        var connection = await Session.GetConnectionAsync(ct);

        return await GuardAsync(async () =>
        {
            var lookup = new Dictionary<long, OrderItem>();
            var ordered = new List<OrderItem>();

            await connection.QueryAsync<OrderItem, OrderItemOption?, OrderItem>(
                Command(SelectWithOptionsSql, new { OrderId = orderId }, ct),
                (item, option) =>
                {
                    if (!lookup.TryGetValue(item.Id, out var existing))
                    {
                        existing = item;
                        lookup[item.Id] = existing;
                        ordered.Add(existing);
                    }

                    if (option is { Id: not 0 })
                        existing.Options.Add(option);

                    return existing;
                },
                splitOn: "Id");

            return (IReadOnlyList<OrderItem>)ordered;
        });
    }

    public async Task<long> AddWithOptionsAsync(OrderItem item, CancellationToken ct = default)
    {
        var id = await AddAsync(item, ct);

        if (item.Options.Count > 0)
        {
            var connection = await Session.GetConnectionAsync(ct);
            var rows = item.Options.Select(option => new
            {
                OrderItemId = id,
                option.OptionId,
                option.OptionGroup,
                option.OptionValue,
                option.PriceDelta
            }).ToList();

            // один виклик — по одному виконанню на кожен елемент колекції, усе параметризовано
            await GuardAsync(() => connection.ExecuteAsync(Command(InsertOptionSql, rows, ct)));
        }

        return id;
    }
}
