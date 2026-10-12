namespace Orders.Domain.Models;

/// <summary>
/// Позиція замовлення (dbo.OrderItems). ProductName і UnitPrice — знімок довідника на момент оформлення.
/// </summary>
public class OrderItem : BaseEntity
{
    public long OrderId { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Units { get; set; }
    public decimal Discount { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<OrderItemOption> Options { get; set; } = new();

    public decimal UnitTotal => UnitPrice + Options.Sum(option => option.PriceDelta) - Discount;

    public decimal LineTotal => UnitTotal * Units;
}
