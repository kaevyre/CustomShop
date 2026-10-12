namespace Orders.Domain.Models;

public class Product : BaseEntity, IVersioned
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime SyncedAt { get; set; }
    public byte[]? RowVersion { get; set; }
}
