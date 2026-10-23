using Catalog.Domain.Exceptions;

namespace Catalog.Domain.Entities;

/// <summary>Варіант налаштування товару (1:N з Product). Id копіюють Замовлення (OrderItemOptions.OptionId).</summary>
public class CustomizationOption
{
    private CustomizationOption()
    {
    }

    public CustomizationOption(long productId, string groupName, string value, decimal priceDelta = 0,
        bool isDefault = false, int sortOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(groupName) || string.IsNullOrWhiteSpace(value))
            throw new DomainException("Option group and value are required.");
        if (priceDelta < 0)
            throw new DomainException("Price delta cannot be negative.");

        ProductId = productId;
        GroupName = groupName.Trim();
        Value = value.Trim();
        PriceDelta = priceDelta;
        IsDefault = isDefault;
        IsAvailable = true;
        SortOrder = sortOrder;
    }

    public long Id { get; private set; }
    public long ProductId { get; private set; }
    public string GroupName { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public decimal PriceDelta { get; private set; }
    public bool IsDefault { get; private set; }
    public bool IsAvailable { get; private set; }
    public int SortOrder { get; private set; }
}
