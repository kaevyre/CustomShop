using System.Data;
using Microsoft.Data.SqlClient;

namespace Orders.Dal.Infrastructure;

/// <summary>Одне з'єднання з БД і поточна транзакція, спільні для всіх репозиторіїв одного Unit of Work.</summary>
public interface IDbSession : IAsyncDisposable
{
    /// <summary>Поточна транзакція або <c>null</c> (тоді кожна команда виконується в autocommit).</summary>
    SqlTransaction? Transaction { get; }

    Task<SqlConnection> GetConnectionAsync(CancellationToken ct = default);

    Task BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}
