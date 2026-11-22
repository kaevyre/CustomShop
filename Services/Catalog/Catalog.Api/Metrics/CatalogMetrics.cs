using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Catalog.Api.Metrics;

/// <summary>
/// Бізнес-метрики сервісу каталогу (Лаб. 07). Meter "Catalog.Api" створюється через IMeterFactory з DI
/// і підключений у Platform.ServiceDefaults (AddMeter).
///
/// Теги (низька кардинальність): product.type — тип товару (клавіатура, миша, ...), change — price | stock_add | stock_remove,
/// operation — get | create | change_price | stock, status — success | failure. Id товарів у тегах не використовуються.
/// Клас реєструється як Singleton.
/// </summary>
public sealed class CatalogMetrics
{
    public const string MeterName = "Catalog.Api";

    private readonly Counter<long> _productsCreated;
    private readonly Counter<long> _productsUpdated;
    private readonly Histogram<double> _operationDuration;

    private int _operationsInProgress;

    public CatalogMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _productsCreated = meter.CreateCounter<long>(
            "products.created", unit: "{product}", description: "Number of created products");
        _productsUpdated = meter.CreateCounter<long>(
            "products.updated", unit: "{product}", description: "Number of product updates by kind of change");
        _operationDuration = meter.CreateHistogram<double>(
            "catalog.operation.duration", unit: "ms", description: "Duration of catalog operations (successful and failed)");

        meter.CreateObservableGauge(
            "catalog.operations.in_progress", () => Volatile.Read(ref _operationsInProgress),
            unit: "{operation}", description: "Catalog operations being executed right now");
    }

    public void ProductCreated(string productType) =>
        _productsCreated.Add(1, new KeyValuePair<string, object?>("product.type", productType));

    public void ProductUpdated(string change) =>
        _productsUpdated.Add(1, new KeyValuePair<string, object?>("change", change));

    public void RecordDuration(string operation, bool success, double ms) =>
        _operationDuration.Record(ms,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("status", success ? "success" : "failure"));

    /// <summary>Виконує операцію й записує тривалість для всіх виконань, включно з невдалими.</summary>
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
}
