using System.Data;
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

    public async Task<bool> UpsertSnapshotAsync(ProductSnapshot snapshot, CancellationToken ct = default)
    {
        const string sql = """
            MERGE dbo.Products WITH (HOLDLOCK) AS t
            USING (SELECT @Id AS Id) AS s ON t.Id = s.Id
            WHEN MATCHED AND t.SyncedAt <= @OccurredAt THEN
                UPDATE SET Sku = @Sku, Name = @Name, Price = @Price, IsActive = @IsActive, SyncedAt = @OccurredAt
            WHEN NOT MATCHED THEN
                INSERT (Id, Sku, Name, Price, Stock, IsActive, SyncedAt)
                VALUES (@Id, @Sku, @Name, @Price, ISNULL(@Stock, 0), @IsActive, @OccurredAt);
            """;

        var parameters = new DynamicParameters();
        parameters.Add("Id", snapshot.Id, DbType.Int64);
        parameters.Add("Sku", snapshot.Sku, DbType.String, size: 50);
        parameters.Add("Name", snapshot.Name, DbType.String, size: 200);
        parameters.Add("Price", snapshot.Price, DbType.Decimal);
        parameters.Add("Stock", snapshot.Stock, DbType.Int32);
        parameters.Add("IsActive", snapshot.IsActive, DbType.Boolean);
        parameters.Add("OccurredAt", snapshot.OccurredAt, DbType.DateTime2);   // не DateTime: datetime округлює до 1/300 с

        var connection = await Session.GetConnectionAsync(ct);
        var affected = await GuardAsync(() => connection.ExecuteAsync(Command(sql, parameters, ct)));
        return affected > 0;
    }

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
