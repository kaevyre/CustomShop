using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Orders.Dal.Abstractions;
using Orders.Dal.Infrastructure;
using Orders.Domain.Exceptions;
using Orders.Domain.Models;

namespace Orders.Dal.Repositories;

/// <summary>
/// Спільна Dapper-реалізація CRUD. Нащадок описує лише таблицю та перелік стовпців —
/// SQL будується один раз із цих констант, а всі значення передаються параметрами (@Name).
/// Усі методи асинхронні й приймають CancellationToken; з'єднання й транзакцію надає <see cref="IDbSession"/>.
/// </summary>
public abstract class BaseDapperRepository<T> : IGenericRepository<T> where T : BaseEntity
{
    private static readonly bool IsVersioned = typeof(IVersioned).IsAssignableFrom(typeof(T));
    private static readonly bool IsAuditable = typeof(AuditableEntity).IsAssignableFrom(typeof(T));

    private string? _selectColumns;
    private string? _selectByIdSql;
    private string? _selectAllSql;
    private string? _selectPagedSql;
    private string? _insertSql;
    private string? _updateSql;
    private string? _deleteSql;

    protected BaseDapperRepository(IDbSession session)
    {
        Session = session;
    }

    protected IDbSession Session { get; }

    protected abstract string TableName { get; }

    protected abstract IReadOnlyList<string> Columns { get; }

    protected virtual IReadOnlyList<string> ReadOnlyColumns => [];

    protected virtual string Schema => "dbo";

    protected virtual bool IdIsGenerated => true;

    protected virtual bool SupportsSoftDelete => false;

    protected string QualifiedTable => SqlBuilder.Table(Schema, TableName);

    protected string SelectColumns =>
        _selectColumns ??= SqlBuilder.ColumnList(new[] { "Id" }.Concat(Columns).Concat(ReadOnlyColumns));

    private string WhereNotDeleted => SupportsSoftDelete ? " WHERE [IsDeleted] = 0" : string.Empty;
    private string AndNotDeleted => SupportsSoftDelete ? " AND [IsDeleted] = 0" : string.Empty;

    private string SelectByIdSql =>
        _selectByIdSql ??= $"SELECT {SelectColumns} FROM {QualifiedTable} WHERE [Id] = @Id{AndNotDeleted}";

    private string SelectAllSql =>
        _selectAllSql ??= $"SELECT {SelectColumns} FROM {QualifiedTable}{WhereNotDeleted} ORDER BY [Id]";

    private string SelectPagedSql =>
        _selectPagedSql ??=
            $"SELECT COUNT(*) FROM {QualifiedTable}{WhereNotDeleted}; " +
            $"SELECT {SelectColumns} FROM {QualifiedTable}{WhereNotDeleted} " +
            "ORDER BY [Id] OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;";

    private string InsertSql
    {
        get
        {
            if (_insertSql is not null)
                return _insertSql;

            if (IdIsGenerated)
            {
                _insertSql =
                    $"INSERT INTO {QualifiedTable} ({SqlBuilder.ColumnList(Columns)}) " +
                    $"OUTPUT INSERTED.[Id] VALUES ({SqlBuilder.ParameterList(Columns)})";
            }
            else
            {
                var all = new[] { "Id" }.Concat(Columns).ToArray();
                _insertSql =
                    $"INSERT INTO {QualifiedTable} ({SqlBuilder.ColumnList(all)}) " +
                    $"VALUES ({SqlBuilder.ParameterList(all)})";
            }

            return _insertSql;
        }
    }

    private string UpdateSql =>
        _updateSql ??=
            $"UPDATE {QualifiedTable} SET {SqlBuilder.SetList(Columns)}" +
            (IsAuditable ? ", [UpdatedAt] = SYSUTCDATETIME()" : string.Empty) +
            $" WHERE [Id] = @Id{AndNotDeleted}" +
            // оптимістична конкурентність: якщо клієнт передав RowVersion, запис мав не змінитися
            (IsVersioned ? " AND (@RowVersion IS NULL OR [RowVersion] = @RowVersion)" : string.Empty);

    private string DeleteSql =>
        _deleteSql ??= SupportsSoftDelete
            ? $"UPDATE {QualifiedTable} SET [IsDeleted] = 1, [UpdatedAt] = SYSUTCDATETIME() WHERE [Id] = @Id AND [IsDeleted] = 0"
            : $"DELETE FROM {QualifiedTable} WHERE [Id] = @Id";

    public virtual async Task<T?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var connection = await Session.GetConnectionAsync(ct);
        return await GuardAsync(() =>
            connection.QuerySingleOrDefaultAsync<T>(Command(SelectByIdSql, new { Id = id }, ct)));
    }

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        var connection = await Session.GetConnectionAsync(ct);
        var rows = await GuardAsync(() => connection.QueryAsync<T>(Command(SelectAllSql, null, ct)));
        return rows.AsList();
    }

    public virtual async Task<PagedResult<T>> GetPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var connection = await Session.GetConnectionAsync(ct);
        var parameters = new { Skip = (page - 1) * pageSize, Take = pageSize };

        return await GuardAsync(async () =>
        {
            using var grid = await connection.QueryMultipleAsync(Command(SelectPagedSql, parameters, ct));
            var total = await grid.ReadSingleAsync<int>();
            var items = (await grid.ReadAsync<T>()).AsList();
            return new PagedResult<T>(items, total, page, pageSize);
        });
    }

    public virtual async Task<long> AddAsync(T entity, CancellationToken ct = default)
    {
        var connection = await Session.GetConnectionAsync(ct);

        if (IdIsGenerated)
        {
            var id = await GuardAsync(() => connection.ExecuteScalarAsync<long>(Command(InsertSql, entity, ct)));
            entity.Id = id;
            return id;
        }

        await GuardAsync(() => connection.ExecuteAsync(Command(InsertSql, entity, ct)));
        return entity.Id;
    }

    public virtual async Task<bool> UpdateAsync(T entity, CancellationToken ct = default)
    {
        var connection = await Session.GetConnectionAsync(ct);
        var affected = await GuardAsync(() => connection.ExecuteAsync(Command(UpdateSql, entity, ct)));
        return affected > 0;
    }

    public virtual async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        var connection = await Session.GetConnectionAsync(ct);
        var affected = await GuardAsync(() => connection.ExecuteAsync(Command(DeleteSql, new { Id = id }, ct)));
        return affected > 0;
    }

    /// <summary>Команда Dapper, прив'язана до поточної транзакції Unit of Work та токена скасування.</summary>
    protected CommandDefinition Command(
        string sql, object? parameters, CancellationToken ct, CommandType? commandType = null) =>
        new(sql, parameters, Session.Transaction, commandType: commandType, cancellationToken: ct);

    protected static async Task<TResult> GuardAsync<TResult>(Func<Task<TResult>> action)
    {
        try
        {
            return await action();
        }
        catch (SqlException ex) when (SqlExceptionTranslator.TryTranslate(ex, out var domain))
        {
            throw domain!;
        }
    }
}
