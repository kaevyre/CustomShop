namespace Orders.Domain.Models;

/// <summary>
/// Обране налаштування позиції (dbo.OrderItemOptions): світчі, колір, принт тощо.
/// OptionId — посилання на CustomizationOptions у БД Каталогу (без FK);
/// OptionGroup, OptionValue, PriceDelta — знімок на момент оформлення.
/// </summary>
public class OrderItemOption : BaseEntity
{
    public long OrderItemId { get; set; }
    public long OptionId { get; set; }
    public string OptionGroup { get; set; } = string.Empty;
    public string OptionValue { get; set; } = string.Empty;
    public decimal PriceDelta { get; set; }
}
