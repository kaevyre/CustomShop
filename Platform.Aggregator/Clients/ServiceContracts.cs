namespace Platform.Aggregator.Clients;

// Контракти відповідей мікросервісів — лише поля, потрібні Aggregator-у (System.Text.Json ігнорує решту).
// Aggregator не посилається на проєкти сервісів: залежність лише від їхнього HTTP API.

// orders-api: GET /api/orders/{id}
public sealed record OrderDto(
    long Id,
    long CustomerId,
    string Status,
    DateTime OrderDate,
    DateTime? PaidAt,
    DateTime? ShippedAt,
    decimal TotalAmount,
    OrderCustomerDto? Customer,
    OrderDeliveryDto? Delivery,
    IReadOnlyList<OrderItemDto>? Items);

public sealed record OrderCustomerDto(long Id, string FullName);

public sealed record OrderDeliveryDto(string RecipientName, string City, string DeliveryMethod);

public sealed record OrderItemDto(
    long Id,
    long ProductId,
    string ProductName,
    decimal UnitPrice,
    int Units,
    decimal Discount,
    decimal LineTotal,
    IReadOnlyList<OrderItemOptionDto>? Options);

public sealed record OrderItemOptionDto(long OptionId, string OptionGroup, string OptionValue, decimal PriceDelta);

// catalog-api: GET /api/products/{id}
public sealed record ProductDto(
    long Id,
    string Sku,
    string Name,
    string ProductType,
    string? BrandName,
    decimal BasePrice,
    int Stock,
    bool IsCustomizable,
    bool IsActive);

// reviews-api: GET /api/reviews?productId=  і  GET /api/reviews/products/{productId}/rating
public sealed record ReviewDto(
    string Id,
    long ProductId,
    string AuthorName,
    int Rating,
    string? Title,
    string Text,
    bool VerifiedPurchase,
    DateTime CreatedAt);

public sealed record ReviewPageDto(IReadOnlyList<ReviewDto>? Items, long TotalCount);

public sealed record ProductRatingDto(long ProductId, int ReviewsCount, double AverageRating);

/// <summary>Відгуки одного товару: рейтинг (null, якщо відгуків ще немає) і кілька останніх.</summary>
public sealed record ProductReviewsDto(long ProductId, ProductRatingDto? Rating, IReadOnlyList<ReviewDto> Latest, long TotalCount);
