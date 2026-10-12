using System.Data;
using Orders.Dal.Abstractions;
using Orders.Dal.Infrastructure;

namespace Orders.Dal;

/// <summary>
/// Unit of Work: усі репозиторії діляться одним <see cref="IDbSession"/> — тобто одним з'єднанням
/// і однією транзакцією. Життєвий цикл Scoped; при знищенні активну транзакцію відкочено,
/// з'єднання закрито.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly IDbSession _session;

    public UnitOfWork(
        IDbSession session,
        ICustomerRepository customers,
        IProductRepository products,
        IOrderRepository orders,
        IOrderItemRepository orderItems,
        IOrderSummaryRepository orderSummaries)
    {
        _session = session;
        Customers = customers;
        Products = products;
        Orders = orders;
        OrderItems = orderItems;
        OrderSummaries = orderSummaries;
    }

    public ICustomerRepository Customers { get; }
    public IProductRepository Products { get; }
    public IOrderRepository Orders { get; }
    public IOrderItemRepository OrderItems { get; }
    public IOrderSummaryRepository OrderSummaries { get; }

    public bool HasActiveTransaction => _session.Transaction is not null;

    public Task BeginAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken ct = default) =>
        _session.BeginTransactionAsync(isolationLevel, ct);

    public Task CommitAsync(CancellationToken ct = default) => _session.CommitAsync(ct);

    public Task RollbackAsync(CancellationToken ct = default) => _session.RollbackAsync(ct);

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<Task<TResult>> work,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken ct = default)
    {
        await BeginAsync(isolationLevel, ct);
        try
        {
            var result = await work();
            await CommitAsync(ct);
            return result;
        }
        catch
        {
            await RollbackAsync();
            throw;
        }
    }

    public ValueTask DisposeAsync() => _session.DisposeAsync();
}
