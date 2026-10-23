using Dapper;
using Orders.Dal.Abstractions;
using Orders.Dal.Infrastructure;
using Orders.Domain.Models;

namespace Orders.Dal.Repositories;

public sealed class ProductRepository : BaseDapperRepository<Product>, IProductRepository
{
    public ProductRepository(IDbSession session) : base(session)
    {
    }

    protected override string TableName => "Products";
    protected override IReadOnlyList<string> Columns => ["Sku", "Name", "Price", "Stock", "IsActive", "SyncedAt"];
    protected override IReadOnlyList<string> ReadOnlyColumns => ["RowVersion"];
    protected override bool IdIsGenerated => false;

    public async Task<Product?> GetBySkuAsync(string sku, CancellationToken ct = default)
    {
        var sql = $"SELECT {SelectColumns} FROM {QualifiedTable} WHERE [Sku] = @Sku";
        var connection = await Session.GetConnectionAsync(ct);
        return await GuardAsync(() =>
            connection.QuerySingleOrDefaultAsync<Product>(Command(sql, new { Sku = sku }, ct)));
    }

    public async Task<IReadOnlyList<Product>> GetByIdsAsync(IEnumerable<long> ids, CancellationToken ct = default)
    {
        var idList = ids.Distinct().ToArray();
        if (idList.Length == 0)
            return [];

        // Dapper розгортає @Ids у (@Ids1, @Ids2, ...) — значення знову ж таки параметри
        var sql = $"SELECT {SelectColumns} FROM {QualifiedTable} WHERE [Id] IN @Ids";
        var connection = await Session.GetConnectionAsync(ct);
        var rows = await GuardAsync(() => connection.QueryAsync<Product>(Command(sql, new { Ids = idList }, ct)));
        return rows.AsList();
    }
}
