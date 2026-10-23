namespace Orders.Domain.Models;

public class OrderSummary
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

public class OrderStatusStatistics
{
    public string Status { get; set; } = string.Empty;
    public int OrdersCount { get; set; }
    public decimal TotalAmount { get; set; }
}

public class OrderStatusResult
{
    public long OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? PaidAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public byte[]? RowVersion { get; set; }
}
