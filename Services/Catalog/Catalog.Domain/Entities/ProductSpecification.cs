using Catalog.Domain.Exceptions;

namespace Catalog.Domain.Entities;

/// <summary>Специфікація товару, 1:1 з Product (PK = FK ProductId).</summary>
public class ProductSpecification
{
    private ProductSpecification()
    {
    }

    public ProductSpecification(long productId, string connectionType, int? weightGrams = null,
        string? dimensions = null, string? material = null, string? color = null, int warrantyMonths = 12)
    {
        if (!CatalogRules.ConnectionTypes.Contains(connectionType))
            throw new DomainException($"Unknown connection type '{connectionType}'.");
        if (weightGrams is <= 0)
            throw new DomainException("Weight must be positive.");
        if (warrantyMonths is < 0 or > 60)
            throw new DomainException("Warranty must be between 0 and 60 months.");

        ProductId = productId;
        ConnectionType = connectionType;
        WeightGrams = weightGrams;
        Dimensions = dimensions;
        Material = material;
        Color = color;
        WarrantyMonths = warrantyMonths;
    }

    public long ProductId { get; private set; }
    public int? WeightGrams { get; private set; }
    public string? Dimensions { get; private set; }
    public string ConnectionType { get; private set; } = string.Empty;
    public string? Material { get; private set; }
    public string? Color { get; private set; }
    public int WarrantyMonths { get; private set; }
}
