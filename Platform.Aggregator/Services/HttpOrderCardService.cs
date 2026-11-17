using System.Diagnostics;
using Platform.Aggregator.Clients;

namespace Platform.Aggregator.Services;

/// <summary>
/// Реалізація Лаб. 05 поверх HTTP REST. Лишається як база для порівняння затримок із gRPC (scripts/compare-http-grpc.ps1);
/// основний шлях для клієнтів — <see cref="OrderCardService"/>.
/// </summary>
public sealed class HttpOrderCardService(
    OrdersClient orders,
    CatalogClient catalog,
    ReviewsClient reviews,
    ILogger<HttpOrderCardService> logger)
{
    public async Task<OrderCardResult> GetFullOrderAsync(long orderId, CancellationToken ct)
    {
        var warnings = new List<string>();

        var order = await SafeAsync(() => orders.GetOrderAsync(orderId, ct), OrdersClient.ServiceName, warnings, ct);
        if (order is null)
            return new OrderCardResult(warnings.Count > 0 ? OrderCardOutcome.OrdersUnavailable : OrderCardOutcome.NotFound);

        var productIds = (order.Items ?? []).Select(i => i.ProductId).Distinct().ToList();

        var productsTask = SafeAsync(() => catalog.GetProductsAsync(productIds, ct), CatalogClient.ServiceName, warnings, ct);
        var reviewsTask = SafeAsync(() => reviews.GetForProductsAsync(productIds, ct), ReviewsClient.ServiceName, warnings, ct);
        await Task.WhenAll(productsTask, reviewsTask);

        var card = OrderCardAssembler.Build(order, await productsTask, await reviewsTask, warnings, logger);
        return new OrderCardResult(OrderCardOutcome.Found, card);
    }

    private async Task<T?> SafeAsync<T>(Func<Task<T>> call, string service, List<string> warnings, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await call();
            logger.LogInformation("{Service} responded in {ElapsedMs} ms via {Transport}", service, sw.ElapsedMilliseconds, "http");
            return result;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "{Service} is unavailable after {ElapsedMs} ms", service, sw.ElapsedMilliseconds);
            lock (warnings) warnings.Add($"{service} is unavailable");
            return default;
        }
    }
}
