using Platform.Aggregator.Clients;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Services;

public enum OrderCardOutcome { Found, NotFound, OrdersUnavailable }

public sealed record OrderCardResult(OrderCardOutcome Outcome, FullOrderDto? Card = null);

/// <summary>Збирає картку замовлення з даних сервісів незалежно від транспорту (gRPC чи HTTP).</summary>
public static class OrderCardAssembler
{
    public static FullOrderDto Build(
        OrderDto order,
        IReadOnlyList<ProductDto>? products,
        IReadOnlyList<ProductReviewsDto>? productReviews,
        List<string> warnings,
        ILogger logger)
    {
        var items = order.Items ?? [];
        var productIds = items.Select(i => i.ProductId).ToHashSet();

        // узгодженість: беремо лише товари/відгуки, що належать позиціям саме цього замовлення
        var productsById = (products ?? []).Where(p => productIds.Contains(p.Id)).ToDictionary(p => p.Id);
        var reviewsById = (productReviews ?? []).Where(r => productIds.Contains(r.ProductId)).ToDictionary(r => r.ProductId);

        if (products is not null)
        {
            foreach (var missing in productIds.Where(id => !productsById.ContainsKey(id)))
            {
                logger.LogWarning("Product {ProductId} of order {OrderId} is missing in catalog-api", missing, order.Id);
                warnings.Add($"Product {missing} is no longer available in catalog-api");
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
            reviewsById.TryGetValue(item.ProductId, out var reviews) ? ToCard(reviews) : null)).ToList();

        logger.LogInformation(
            "Order card {OrderId} built: {ItemsCount} items, status {Status}, {WarningsCount} warnings",
            order.Id, lines.Count, order.Status, warnings.Count);

        return new FullOrderDto(
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
