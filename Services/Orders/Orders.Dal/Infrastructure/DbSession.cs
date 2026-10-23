using System.Data;
using Microsoft.Data.SqlClient;

namespace Orders.Dal.Infrastructure;

public sealed class DbSession : IDbSession
{
    private readonly string _connectionString;
    private SqlConnection? _connection;

    public DbSession(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string is empty.", nameof(connectionString));
        _connectionString = connectionString;
    }

    public SqlTransaction? Transaction { get; private set; }

    public async Task<SqlConnection> GetConnectionAsync(CancellationToken ct = default)
    {
        _connection ??= new SqlConnection(_connectionString);
        if (_connection.State != ConnectionState.Open)
            await _connection.OpenAsync(ct);
        return _connection;
    }

    public async Task BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken ct = default)
    {
        if (Transaction is not null)
            throw new InvalidOperationException("A transaction is already active in this Unit of Work.");

        var connection = await GetConnectionAsync(ct);
        Transaction = (SqlTransaction)await connection.BeginTransactionAsync(isolationLevel, ct);
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        var transaction = Transaction ?? throw new InvalidOperationException("There is no active transaction.");
        try
        {
            await transaction.CommitAsync(ct);
        }
        finally
        {
            await ReleaseTransactionAsync();
        }
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        var transaction = Transaction;
        if (transaction is null)
            return;

        try
        {
            // Якщо сервер уже сам скасував транзакцію (XACT_ABORT у збереженій процедурі),
            // повторний Rollback кидає виняток — він нам не потрібен.
            if (transaction.Connection is not null)
                await transaction.RollbackAsync(ct);
        }
        catch (InvalidOperationException)
        {
        }
        catch (SqlException)
        {
        }
        finally
        {
            await ReleaseTransactionAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Transaction is not null)
            await RollbackAsync();

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    private async Task ReleaseTransactionAsync()
    {
        if (Transaction is not null)
        {
            await Transaction.DisposeAsync();
            Transaction = null;
        }
    }
}
