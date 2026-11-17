using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Reviews.WebApi.Metrics;

/// <summary>
/// Бізнес-метрики сервісу відгуків (Лаб. 07). Meter "Reviews.Api" створюється через IMeterFactory з DI
/// і підключений у Platform.ServiceDefaults (AddMeter).
///
/// Теги (низька кардинальність): review.rating — оцінка 1..5, operation — list | rating | create, status — success | failure.
/// Ідентифікатори товарів, авторів і відгуків у тегах не використовуються. Клас реєструється як Singleton.
/// </summary>
public sealed class ReviewMetrics
{
    public const string MeterName = "Reviews.Api";

    private readonly Counter<long> _reviewsCreated;
    private readonly Histogram<double> _operationDuration;

    private int _operationsInProgress;

    public ReviewMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _reviewsCreated = meter.CreateCounter<long>(
            "reviews.created", unit: "{review}", description: "Number of created reviews");
        _operationDuration = meter.CreateHistogram<double>(
            "reviews.operation.duration", unit: "ms", description: "Duration of review operations (successful and failed)");

        meter.CreateObservableGauge(
            "reviews.operations.in_progress", () => Volatile.Read(ref _operationsInProgress),
            unit: "{operation}", description: "Review operations being executed right now");
    }

    public void ReviewCreated(int rating) =>
        _reviewsCreated.Add(1, new KeyValuePair<string, object?>("review.rating", rating));

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
