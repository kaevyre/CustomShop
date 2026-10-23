using System.Data;

namespace Orders.Dal.Abstractions;

/// <summary>
/// Unit of Work: усі репозиторії працюють через ОДНЕ з'єднання й (за потреби) ОДНУ транзакцію.
/// Реєструється як Scoped — одне з'єднання на HTTP-запит.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    ICustomerRepository Customers { get; }
    IProductRepository Products { get; }
    IOrderRepository Orders { get; }
    IOrderItemRepository OrderItems { get; }
    IOrderSummaryRepository OrderSummaries { get; }

    bool HasActiveTransaction { get; }

    Task BeginAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);

    /// <summary>
    /// Виконує <paramref name="work"/> в одній транзакції: успіх → Commit, будь-який виняток → Rollback і повторний throw.
    /// </summary>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<Task<TResult>> work,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken ct = default);
}
