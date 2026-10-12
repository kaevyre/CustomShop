namespace Orders.Domain.Models;

public class Order : AuditableEntity
{
    public long CustomerId { get; set; }

    public string Status { get; set; } = OrderStatus.Submitted;

    public DateTime OrderDate { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? ShippedAt { get; set; }

    // Навігаційні властивості — заповнюються репозиторіями окремими запитами (multi-mapping).
    public OrderDetails? Details { get; set; }
    public Customer? Customer { get; set; }
    public List<OrderItem> Items { get; set; } = new();

    public decimal TotalAmount => Items.Sum(item => item.LineTotal);
}
