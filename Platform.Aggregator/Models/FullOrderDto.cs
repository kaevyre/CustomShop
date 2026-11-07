namespace Platform.Aggregator.Models;

// Композитна відповідь під бізнес-сценарій «повна картка замовлення»: не копія структур окремих сервісів,
// а те, що потрібно екрану: позиція замовлення (знімок з orders-api) + актуальний стан товару (catalog-api)
// + рейтинг і свіжі відгуки (reviews-api). Якщо джерело недоступне, його частина відсутня (null не серіалізується),
// а причина — у Warnings. JSON — camelCase.

public sealed record FullOrderDto(
    long OrderId,
    string Status,
    DateTime OrderDate,
    DateTime? PaidAt,
    DateTime? ShippedAt,
    decimal TotalAmount,
    OrderCustomerCard? Customer,
    OrderDeliveryCard? Delivery,
    IReadOnlyList<OrderLineCard> Items,
    bool IsComplete,
    IReadOnlyList<string> Warnings);

/// <summary>Лише ім'я покупця: email і телефон у картку не потрапляють (мінімум персональних даних).</summary>
public sealed record OrderCustomerCard(long Id, string FullName);

public sealed record OrderDeliveryCard(string RecipientName, string City, string DeliveryMethod);

/// <summary>Позиція замовлення: назва й ціна — знімок на момент оформлення (orders-api).</summary>
public sealed record OrderLineCard(
    long ProductId,
    string ProductName,
    decimal UnitPrice,
    int Units,
    decimal Discount,
    decimal LineTotal,
    IReadOnlyList<string> Options,
    CatalogProductCard? Product,
    ProductReviewsCard? Reviews);

/// <summary>Актуальні дані товару з catalog-api.</summary>
public sealed record CatalogProductCard(
    string Sku,
    string CurrentName,
    string ProductType,
    string? Brand,
    decimal CurrentPrice,
    bool PriceChangedSinceOrder,
    int Stock,
    bool InStock,
    bool IsActive);

/// <summary>Відгуки на товар з reviews-api.</summary>
public sealed record ProductReviewsCard(
    double? AverageRating,
    long ReviewsCount,
    IReadOnlyList<ReviewCard> Latest);

public sealed record ReviewCard(
    string Id,
    string Author,
    int Rating,
    string? Title,
    string Text,
    bool VerifiedPurchase,
    DateTime CreatedAt);
