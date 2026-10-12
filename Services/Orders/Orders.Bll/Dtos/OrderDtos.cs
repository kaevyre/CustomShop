using System.ComponentModel.DataAnnotations;

namespace Orders.Bll.Dtos;

public sealed class DeliveryDto
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string RecipientName { get; set; } = string.Empty;

    [Required, Phone, StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string City { get; set; } = string.Empty;

    [Required, StringLength(400)]
    public string ShippingAddress { get; set; } = string.Empty;

    [RegularExpression("^(NovaPoshta|UkrPoshta|Courier|Pickup)$",
        ErrorMessage = "DeliveryMethod must be one of: NovaPoshta, UkrPoshta, Courier, Pickup.")]
    public string DeliveryMethod { get; set; } = "NovaPoshta";

    [StringLength(50)]
    public string? EngravingText { get; set; }

    [StringLength(1000)]
    public string? Comment { get; set; }
}

public sealed class CreateOrderItemOptionDto
{
    [Range(1, long.MaxValue)]
    public long OptionId { get; set; }

    [Required, StringLength(100)]
    public string Group { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Value { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "999999999999999", ParseLimitsInInvariantCulture = true)]
    public decimal PriceDelta { get; set; }
}

public sealed class CreateOrderItemDto
{
    [Range(1, long.MaxValue)]
    public long ProductId { get; set; }

    [Range(1, 50)]
    public int Units { get; set; } = 1;

    [Range(typeof(decimal), "0", "999999999999999", ParseLimitsInInvariantCulture = true)]
    public decimal Discount { get; set; }

    public List<CreateOrderItemOptionDto> Options { get; set; } = new();
}

public sealed class CreateOrderDto
{
    [Range(1, long.MaxValue)]
    public long CustomerId { get; set; }

    [Required]
    public DeliveryDto Delivery { get; set; } = new();

    [Required, MinLength(1, ErrorMessage = "An order must contain at least one item.")]
    public List<CreateOrderItemDto> Items { get; set; } = new();
}

public sealed class ChangeOrderStatusDto
{
    [Required]
    public string NewStatus { get; set; } = string.Empty;

    public string? RowVersion { get; set; }
}

public sealed class OrderItemOptionDto
{
    public long OptionId { get; set; }
    public string OptionGroup { get; set; } = string.Empty;
    public string OptionValue { get; set; } = string.Empty;
    public decimal PriceDelta { get; set; }
}

public sealed class OrderItemDto
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Units { get; set; }
    public decimal Discount { get; set; }
    public decimal LineTotal { get; set; }
    public List<OrderItemOptionDto> Options { get; set; } = new();
}

public sealed class OrderDto
{
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public string? RowVersion { get; set; }
    public CustomerSummaryDto? Customer { get; set; }
    public DeliveryDto? Delivery { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
}

public sealed class OrderSummaryDto
{
    public long OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public string? City { get; set; }
    public string? DeliveryMethod { get; set; }
    public int ItemsCount { get; set; }
    public int UnitsTotal { get; set; }
    public decimal TotalAmount { get; set; }
}

public sealed class OrderStatusResultDto
{
    public long OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? PaidAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public string? RowVersion { get; set; }
}

public sealed class OrderStatusStatisticsDto
{
    public string Status { get; set; } = string.Empty;
    public int OrdersCount { get; set; }
    public decimal TotalAmount { get; set; }
}
