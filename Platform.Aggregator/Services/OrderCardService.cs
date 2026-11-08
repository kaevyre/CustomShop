using System.Diagnostics;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Services;

public enum OrderCardOutcome { Found, NotFound, OrdersUnavailable }

public sealed record OrderCardResult(OrderCardOutcome Outcome, FullOrderDto? Card = null);

/// <summary>
/// Збирає повну картку замовлення: спершу замовлення (без нього агрегувати нічого), потім паралельно
/// (<see cref="Task.WhenAll(Task[])"/>) товари з catalog-api і відгуки з reviews-api.
/// Недоступність catalog-api чи reviews-api не «валить» запит: повертаються доступні дані + Warnings.
/// </summary>
public sealed class OrderCardService(
    OrdersClient orders,
    CatalogClient catalog,
    ReviewsClient reviews,
    ILogger<OrderCardService> logger)
{
    private static readonly ActivitySource Source = new("Platform.Aggregator");

    public async Task<OrderCardResult> GetFullOrderAsync(long orderId, CancellationToken ct)
    {
        using var activity = Source.StartActivity("BuildOrderCard");
        activity?.SetTag("order.id", orderId);

        var warnings = new List<string>();

        var order = await SafeAsync(() => orders.GetOrderAsync(orderId, ct), OrdersClient.ServiceName, warnings, ct);
        if (order is null)
        {
            if (warnings.Count > 0)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "orders-api unavailable");
                return new OrderCardResult(OrderCardOutcome.OrdersUnavailable);
            }

            logger.LogInformation("Order {OrderId} not found, nothing to aggregate", orderId);
            return new OrderCardResult(OrderCardOutcome.NotFound);
        }

        var items = order.Items ?? [];
        var productIds = items.Select(i => i.ProductId).Distinct().ToList();

        // незалежні виклики — одночасно: загальний час ≈ найповільніший сервіс, а не сума
        var productsTask = SafeAsync(() => catalog.GetProductsAsync(productIds, ct), CatalogClient.ServiceName, warnings, ct);
        var reviewsTask = SafeAsync(() => reviews.GetForProductsAsync(productIds, ct), ReviewsClient.ServiceName, warnings, ct);
        await Task.WhenAll(productsTask, reviewsTask);

        var products = await productsTask;
        var productReviews = await reviewsTask;

        // узгодженість: беремо лише товари/відгуки, що належать позиціям саме цього замовлення
        var productsById = (products ?? [])
            .Where(p => productIds.Contains(p.Id))
            .ToDictionary(p => p.Id);
        var reviewsById = (productReviews ?? [])
            .Where(r => productIds.Contains(r.ProductId))
            .ToDictionary(r => r.ProductId);

        if (products is not null)
        {
            foreach (var missing in productIds.Where(id => !productsById.ContainsKey(id)))
            {
                logger.LogWarning("Product {ProductId} of order {OrderId} is missing in catalog-api", missing, orderId);
                lock (warnings) warnings.Add($"Product {missing} is no longer available in catalog-api");
            }
        }

        var lines = items.Select(item => new OrderLineCard(
            item.ProductId,
            item.ProductName,
            item.UnitPrice,
            item.Units,
            item.Discount,
            item.LineTotal,
            (item.Options ?? []).Select(o => $"{o.OptionGroup}: {o.OptionValue}").ToList(),
            productsById.TryGetValue(item.ProductId, out var product) ? ToCard(product, item) : null,
            reviewsById.TryGetValue(item.ProductId, out var r) ? ToCard(r) : null)).ToList();

        var card = new FullOrderDto(
            order.Id,
            order.Status,
            order.OrderDate,
            order.PaidAt,
            order.ShippedAt,
            order.TotalAmount,
            order.Customer is { } c ? new OrderCustomerCard(c.Id, c.FullName) : null,
            order.Delivery is { } d ? new OrderDeliveryCard(d.RecipientName, d.City, d.DeliveryMethod) : null,
            lines,
            IsComplete: warnings.Count == 0,
            warnings);

        activity?.SetTag("order.items", lines.Count);
        activity?.SetTag("aggregator.warnings", warnings.Count);
        logger.LogInformation(
            "Order card {OrderId} built: {ItemsCount} items, status {Status}, {WarningsCount} warnings",
            order.Id, lines.Count, order.Status, warnings.Count);

        return new OrderCardResult(OrderCardOutcome.Found, card);
    }

    /// <summary>
    /// Виклик сервісу з вимірюванням тривалості. Будь-яка відмова (мережа, 5xx, тайм-аут/circuit breaker resilience,
    /// некоректний JSON) → попередження замість винятку. Скасування самим клієнтом не перехоплюється.
    /// </summary>
    private async Task<T?> SafeAsync<T>(Func<Task<T>> call, string service, List<string> warnings, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await call();
            logger.LogInformation("{Service} responded in {ElapsedMs} ms", service, sw.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "{Service} is unavailable after {ElapsedMs} ms: {Error}",
                service, sw.ElapsedMilliseconds, ex.Message);
            lock (warnings) warnings.Add($"{service} is unavailable");
            Activity.Current?.AddEvent(new ActivityEvent($"{service} unavailable"));
            return default;
        }
    }

    private static CatalogProductCard ToCard(ProductDto p, OrderItemDto item) => new(
        p.Sku,
        p.Name,
        p.ProductType,
        p.BrandName,
        p.BasePrice,
        PriceChangedSinceOrder: p.BasePrice != item.UnitPrice,
        p.Stock,
        InStock: p.IsActive && p.Stock > 0,
        p.IsActive);

    private static ProductReviewsCard ToCard(ProductReviewsDto r) => new(
        r.Rating is null ? null : Math.Round(r.Rating.AverageRating, 2),
        r.TotalCount,
        r.Latest
            .Where(x => x.ProductId == r.ProductId)
            .Select(x => new ReviewCard(x.Id, x.AuthorName, x.Rating, x.Title, x.Text, x.VerifiedPurchase, x.CreatedAt))
            .ToList());
}
