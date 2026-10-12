namespace Orders.Domain.Models;

public class OrderDetails
{
    public long OrderId { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public string DeliveryMethod { get; set; } = "NovaPoshta";
    public string? EngravingText { get; set; }
    public string? Comment { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
