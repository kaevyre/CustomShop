using System.Data;
using Dapper;
using Orders.Dal.Abstractions;
using Orders.Dal.Infrastructure;
using Orders.Domain.Models;

namespace Orders.Dal.Repositories;

/// <summary>
/// Dapper-репозиторій покупців. Читання — SQL з параметрами; створення, оновлення і видалення
/// викликають збережені процедури Лаб. 01 (usp_UpsertCustomer, usp_DeleteCustomer),
/// які самі перевіряють унікальність email, RowVersion та активні замовлення.
/// </summary>
public sealed class CustomerRepository : BaseDapperRepository<Customer>, ICustomerRepository
{
    public CustomerRepository(IDbSession session) : base(session)
    {
    }

    protected override string TableName => "Customers";
    protected override IReadOnlyList<string> Columns => ["Email", "FullName", "Phone"];
    protected override IReadOnlyList<string> ReadOnlyColumns =>
        ["CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy", "IsDeleted", "RowVersion"];
    protected override bool SupportsSoftDelete => true;

    public async Task<Customer?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var sql = $"SELECT {SelectColumns} FROM {QualifiedTable} WHERE [Email] = @Email AND [IsDeleted] = 0";
        var connection = await Session.GetConnectionAsync(ct);
        return await GuardAsync(() =>
            connection.QuerySingleOrDefaultAsync<Customer>(Command(sql, new { Email = email }, ct)));
    }

    public override async Task<long> AddAsync(Customer entity, CancellationToken ct = default)
    {
        var parameters = BuildUpsertParameters(entity, id: null, expectedRowVersion: null, user: entity.CreatedBy);
        var connection = await Session.GetConnectionAsync(ct);

        await GuardAsync(() => connection.ExecuteAsync(
            Command("dbo.usp_UpsertCustomer", parameters, ct, CommandType.StoredProcedure)));

        entity.Id = parameters.Get<long>("Id");
        return entity.Id;
    }

    public override async Task<bool> UpdateAsync(Customer entity, CancellationToken ct = default)
    {
        var parameters = BuildUpsertParameters(entity, entity.Id, entity.RowVersion, entity.UpdatedBy);
        var connection = await Session.GetConnectionAsync(ct);

        await GuardAsync(() => connection.ExecuteAsync(
            Command("dbo.usp_UpsertCustomer", parameters, ct, CommandType.StoredProcedure)));
        return true;
    }

    public override async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Id", id, DbType.Int64);
        parameters.Add("User", null, DbType.String, size: 100);

        var connection = await Session.GetConnectionAsync(ct);
        await GuardAsync(() => connection.ExecuteAsync(
            Command("dbo.usp_DeleteCustomer", parameters, ct, CommandType.StoredProcedure)));
        return true;
    }

    private static DynamicParameters BuildUpsertParameters(
        Customer customer, long? id, byte[]? expectedRowVersion, string? user)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Id", id, DbType.Int64, ParameterDirection.InputOutput);
        parameters.Add("Email", customer.Email, DbType.String, size: 256);
        parameters.Add("FullName", customer.FullName, DbType.String, size: 200);
        parameters.Add("Phone", customer.Phone, DbType.String, size: 20);
        parameters.Add("ExpectedRowVersion", expectedRowVersion, DbType.Binary, size: 8);
        parameters.Add("User", user, DbType.String, size: 100);
        return parameters;
    }
}
