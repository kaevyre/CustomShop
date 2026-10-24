using Catalog.Domain.Exceptions;

namespace Catalog.Domain.Entities;

/// <summary>
/// Корінь агрегату «Товар» (аналог CatalogItem з eShop). Стан змінюється лише методами:
/// ціна перевіряється в конструкторі та в ChangePrice, залишок — через AddStock / RemoveStock.
/// </summary>
public class Product : AuditableEntity
{
    private readonly List<CustomizationOption> _options = new();
    private readonly List<ProductCategory> _productCategories = new();

    private Product()
    {
    }

    public Product(int brandId, string sku, string name, string slug, string productType, decimal basePrice,
        string? description = null, bool isCustomizable = false)
    {
        if (brandId <= 0)
            throw new DomainException("Brand is required.");
        if (string.IsNullOrWhiteSpace(sku))
            throw new DomainException("SKU is required.");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Product name is required.");
        if (!CatalogRules.IsValidSlug(slug))
            throw new DomainException("Product slug may contain only a-z, 0-9 and '-'.");
        if (!CatalogRules.ProductTypes.Contains(productType))
            throw new DomainException($"Unknown product type '{productType}'.");

        BrandId = brandId;
        Sku = sku.Trim();
        Name = name.Trim();
        Slug = slug;
        ProductType = productType;
        Description = description;
        IsCustomizable = isCustomizable;
        IsActive = true;
        ChangePrice(basePrice);
    }

    public long Id { get; private set; }
    public int BrandId { get; private set; }
    public Brand? Brand { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string ProductType { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public decimal BasePrice { get; private set; }
    public int Stock { get; private set; }
    public bool IsCustomizable { get; private set; }
    public bool IsActive { get; private set; }
    public byte[]? RowVersion { get; private set; }

    public ProductSpecification? Specification { get; private set; }                 // 1:1
    public IReadOnlyCollection<CustomizationOption> Options => _options;               // 1:N
    public IReadOnlyCollection<ProductCategory> ProductCategories => _productCategories; // M:N

    public void ChangePrice(decimal newPrice)
    {
        if (newPrice <= 0)
            throw new DomainException("Price must be positive.");
        BasePrice = newPrice;
    }

    public void AddStock(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Quantity must be positive.");
        Stock = checked(Stock + quantity);
    }

    public void RemoveStock(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Quantity must be positive.");
        if (Stock < quantity)
            throw new DomainException($"Only {Stock} units of '{Name}' available.");
        Stock -= quantity;
    }

    public ProductCategory AddToCategory(int categoryId, bool isFeatured, DateTime addedAtUtc)
    {
        if (_productCategories.Any(pc => pc.CategoryId == categoryId))
            throw new DomainException($"Product '{Name}' is already in category {categoryId}.");

        var link = new ProductCategory(Id, categoryId, addedAtUtc, isFeatured);
        _productCategories.Add(link);
        return link;
    }

    public void RemoveFromCategory(int categoryId)
    {
        var link = _productCategories.FirstOrDefault(pc => pc.CategoryId == categoryId)
            ?? throw new EntityNotFoundException(nameof(ProductCategory), $"{Id}/{categoryId}");
        _productCategories.Remove(link);
    }

    public void Delete()
    {
        IsActive = false;
        MarkDeleted();
    }
}
