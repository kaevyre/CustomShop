using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Orders.Api.Metrics;

/// <summary>
/// Бізнес-метрики сервісу замовлень (Лаб. 07). Meter "Orders.Api" створюється через IMeterFactory з DI
/// і підключений у Platform.ServiceDefaults (AddMeter) → вкладка Metrics в Aspire Dashboard.
///
/// Теги (низька кардинальність, однакові назви в усіх сервісах):
///   order.status — статус замовлення (7 значень з OrderStatus);
///   operation    — get | place | change_status | statistics;
///   status       — success | failure (результат операції).
/// Ідентифікатори замовлень і клієнтів у тегах не використовуються: кожен унікальний Id створив би окремий часовий ряд.
/// Клас реєструється як Singleton і живе весь час роботи застосунку.
/// </summary>
public sealed class OrderMetrics
{
    public const string MeterName = "Orders.Api";

    private readonly Counter<long> _ordersCreated;
    private readonly Counter<long> _statusChanged;
    private readonly Histogram<double> _operationDuration;

    private int _operationsInProgress;
    private volatile KeyValuePair<string, int>[] _ordersByStatus = [];

    public OrderMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _ordersCreated = meter.CreateCounter<long>(
            "orders.created", unit: "{order}", description: "Number of created orders");
        _statusChanged = meter.CreateCounter<long>(
            "orders.status_changed", unit: "{order}", description: "Number of order status transitions by new status");
        _operationDuration = meter.CreateHistogram<double>(
            "orders.operation.duration", unit: "ms", description: "Duration of order operations (successful and failed)");

        // gauge: знімок поточного стану, значення читається callback-ом у момент збирання метрик
        meter.CreateObservableGauge(
            "orders.operations.in_progress", () => Volatile.Read(ref _operationsInProgress),
            unit: "{operation}", description: "Order operations being executed right now");
        meter.CreateObservableGauge(
            "orders.by_status", ObserveOrdersByStatus,
            unit: "{order}", description: "Current number of orders per status (snapshot, refreshed periodically)");
    }

    /// <summary>Лічильник збільшується одразу після успішного створення замовлення.</summary>
    public void OrderCreated(string status) =>
        _ordersCreated.Add(1, new KeyValuePair<string, object?>("order.status", status));

    public void StatusChanged(string newStatus) =>
        _statusChanged.Add(1, new KeyValuePair<string, object?>("order.status", newStatus));

    public void RecordDuration(string operation, bool success, double ms) =>
        _operationDuration.Record(ms,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("status", success ? "success" : "failure"));

    /// <summary>
    /// Виконує операцію й записує тривалість для ВСІХ виконань, включно з невдалими
    /// (інакше картина продуктивності викривлена), та веде gauge активних операцій.
    /// </summary>
    public async Task<T> MeasureAsync<T>(string operation, Func<Task<T>> action)
    {
        Interlocked.Increment(ref _operationsInProgress);
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await action();
            RecordDuration(operation, success: true, sw.Elapsed.TotalMilliseconds);
            return result;
        }
        catch
        {
            RecordDuration(operation, success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
        finally
        {
            Interlocked.Decrement(ref _operationsInProgress);
        }
    }

    /// <summary>Оновлює знімок «кількість замовлень за статусами» (викликає OrderStatusSnapshotService).</summary>
    public void UpdateSnapshot(IEnumerable<KeyValuePair<string, int>> ordersByStatus) =>
        _ordersByStatus = ordersByStatus.ToArray();

    private IEnumerable<Measurement<int>> ObserveOrdersByStatus() =>
        _ordersByStatus.Select(item => new Measurement<int>(item.Value, new KeyValuePair<string, object?>("order.status", item.Key)));
}
