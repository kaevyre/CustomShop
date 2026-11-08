using System.Diagnostics;
using Catalog.Grpc;
using Grpc.Core;
using Orders.Grpc;
using Platform.Aggregator.Caching;
using Platform.Aggregator.Clients;
using Reviews.Grpc;
using CatalogMoney = Catalog.Grpc.Money;
using OrdersMoney = Orders.Grpc.Money;

namespace Platform.Aggregator.Services;

/// <summary>
/// Повна картка замовлення через gRPC: спершу кеш Redis, далі замовлення (без нього агрегувати нічого),
/// потім паралельно (<see cref="Task.WhenAll(Task[])"/>) товари й відгуки в межах одного HTTP/2-з'єднання на сервіс.
/// Недоступність catalog-api чи reviews-api не «валить» запит: повертаються доступні дані + Warnings.
/// </summary>
public sealed class OrderCardService(
    OrdersService.OrdersServiceClient orders,
    CatalogService.CatalogServiceClient catalog,
    ReviewsService.ReviewsServiceClient reviews,
    OrderCardCache cache,
    ILogger<OrderCardService> logger)
{
    private const string OrdersApi = "orders-api";
    private const string CatalogApi = "catalog-api";
    private const string ReviewsApi = "reviews-api";

    private static readonly ActivitySource Source = new("Platform.Aggregator");
    private static readonly TimeSpan CallDeadline = TimeSpan.FromSeconds(4);

    public async Task<OrderCardResult> GetFullOrderAsync(long orderId, CancellationToken ct, bool useCache = true)
    {
        using var activity = Source.StartActivity("BuildOrderCard");
        activity?.SetTag("order.id", orderId);

        if (useCache && await cache.GetAsync(orderId, ct) is { } cached)      // ДО gRPC-викликів
        {
            activity?.SetTag("cache.hit", true);
            return new OrderCardResult(OrderCardOutcome.Found, cached);
        }

        activity?.SetTag("cache.hit", false);
        var warnings = new List<string>();

        var order = await SafeAsync(
            () => orders.GetOrderAsync(new GetOrderRequest { Id = orderId }, deadline: Deadline(), cancellationToken: ct).ResponseAsync,
            OrdersApi, warnings, ct);
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

        var productIds = order.Items.Select(i => i.ProductId).Distinct().ToList();

        // незалежні виклики — одночасно: загальний час ≈ найповільніший сервіс, а не сума
        var productsTask = SafeAsync(
            () => catalog.GetProductsByIdsAsync(new GetProductsByIdsRequest { Ids = { productIds } },
                deadline: Deadline(), cancellationToken: ct).ResponseAsync,
            CatalogApi, warnings, ct);
        var reviewsTask = SafeAsync(
            () => reviews.GetForProductsAsync(new GetForProductsRequest { ProductIds = { productIds } },
                deadline: Deadline(), cancellationToken: ct).ResponseAsync,
            ReviewsApi, warnings, ct);
        await Task.WhenAll(productsTask, reviewsTask);

        var products = (await productsTask)?.Products.Select(ToDto).ToList();
        var productReviews = (await reviewsTask)?.Products.Select(ToDto).ToList();

        var card = OrderCardAssembler.Build(ToDto(order), products, productReviews, warnings, logger);
        activity?.SetTag("order.items", card.Items.Count);
        activity?.SetTag("aggregator.warnings", warnings.Count);

        if (useCache)
            await cache.SetAsync(card, ct);                                   // часткові відповіді не кешуються

        return new OrderCardResult(OrderCardOutcome.Found, card);
    }

    private static DateTime Deadline() => DateTime.UtcNow.Add(CallDeadline);

    /// <summary>
    /// gRPC-виклик із вимірюванням тривалості. NotFound — не збій (результат null без попередження);
    /// відмова сервісу → попередження замість 500. Скасування самим клієнтом не перехоплюється.
    /// </summary>
    private async Task<T?> SafeAsync<T>(Func<Task<T>> call, string service, List<string> warnings, CancellationToken ct)
        where T : class
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await call();
            logger.LogInformation("{Service} responded in {ElapsedMs} ms via {Transport}", service, sw.ElapsedMilliseconds, "grpc");
            return result;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            logger.LogInformation("{Service} returned {GrpcStatus} in {ElapsedMs} ms", service, ex.StatusCode, sw.ElapsedMilliseconds);
            return null;
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
        {
            logger.LogWarning(ex, "{Service} is unavailable: {GrpcStatus} after {ElapsedMs} ms", service, ex.StatusCode, sw.ElapsedMilliseconds);
            AddWarning(service, warnings);
            return null;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // напр. тайм-аут чи circuit breaker resilience-конвеєра, які gRPC-клієнт віддає як Internal
            logger.LogWarning(ex, "{Service} call failed after {ElapsedMs} ms", service, sw.ElapsedMilliseconds);
            AddWarning(service, warnings);
            return null;
        }
    }

    private static void AddWarning(string service, List<string> warnings)
    {
        lock (warnings) warnings.Add($"{service} is unavailable");
        Activity.Current?.AddEvent(new ActivityEvent($"{service} unavailable"));
    }

    // gRPC-повідомлення → внутрішні контракти, спільні для обох транспортів
    private static OrderDto ToDto(OrderReply o) => new(
        o.Id,
        o.CustomerId,
        o.Status,
        o.OrderDate.ToDateTime(),
        o.PaidAt?.ToDateTime(),
        o.ShippedAt?.ToDateTime(),
        ToDecimal(o.TotalAmount),
        o.Customer is { } c ? new OrderCustomerDto(c.Id, c.FullName) : null,
        o.Delivery is { } d ? new OrderDeliveryDto(d.RecipientName, d.City, d.DeliveryMethod) : null,
        o.Items.Select(i => new OrderItemDto(
            i.Id,
            i.ProductId,
            i.ProductName,
            ToDecimal(i.UnitPrice),
            i.Units,
            ToDecimal(i.Discount),
            ToDecimal(i.LineTotal),
            i.Options.Select(x => new OrderItemOptionDto(x.OptionId, x.OptionGroup, x.OptionValue, ToDecimal(x.PriceDelta))).ToList()))
        .ToList());

    private static ProductDto ToDto(ProductReply p) => new(
        p.Id,
        p.Sku,
        p.Name,
        p.ProductType,
        p.HasBrandName ? p.BrandName : null,
        ToDecimal(p.Price),
        p.Stock,
        p.IsCustomizable,
        p.IsActive);

    private static ProductReviewsDto ToDto(ProductReviews r) => new(
        r.ProductId,
        r.HasAverageRating ? new ProductRatingDto(r.ProductId, (int)r.TotalCount, r.AverageRating) : null,
        r.Latest.Select(x => new ReviewDto(
            x.Id,
            x.ProductId,
            x.AuthorName,
            x.Rating,
            x.HasTitle ? x.Title : null,
            x.Text,
            x.VerifiedPurchase,
            x.CreatedAt.ToDateTime())).ToList(),
        r.TotalCount);

    private static decimal ToDecimal(OrdersMoney? m) => m is null ? 0 : m.Units + m.Nanos / 1_000_000_000m;

    private static decimal ToDecimal(CatalogMoney? m) => m is null ? 0 : m.Units + m.Nanos / 1_000_000_000m;
}
