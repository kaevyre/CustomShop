using System.ComponentModel.DataAnnotations;

namespace Catalog.Bll.Dtos;

public class ProductDto
{
    public long Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;
    public int BrandId { get; set; }
    public string? BrandName { get; set; }
    public decimal BasePrice { get; set; }
    public int Stock { get; set; }
    public bool IsCustomizable { get; set; }
    public bool IsActive { get; set; }
    public string? RowVersion { get; set; }
}

public sealed class ProductDetailsDto : ProductDto
{
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public ProductSpecificationDto? Specification { get; set; }
    public List<CustomizationOptionDto> Options { get; set; } = new();
    public List<ProductCategoryDto> Categories { get; set; } = new();
}

public sealed class ProductSpecificationDto
{
    public int? WeightGrams { get; set; }
    public string? Dimensions { get; set; }
    public string ConnectionType { get; set; } = string.Empty;
    public string? Material { get; set; }
    public string? Color { get; set; }
    public int WarrantyMonths { get; set; }
}

public sealed class CustomizationOptionDto
{
    public long Id { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public decimal PriceDelta { get; set; }
    public bool IsDefault { get; set; }
    public bool IsAvailable { get; set; }
}

public sealed class ProductCategoryDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string CategorySlug { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; }
    public bool IsFeatured { get; set; }
}

public sealed class CreateProductDto
{
    [Range(1, int.MaxValue)]
    public int BrandId { get; set; }

    [Required, StringLength(50)]
    public string Sku { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Slug { get; set; } = string.Empty;

    [Required]
    public string ProductType { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public decimal BasePrice { get; set; }

    [Range(0, 100_000)]
    public int InitialStock { get; set; }

    public bool IsCustomizable { get; set; }
}

public sealed class ChangePriceDto
{
    public decimal Price { get; set; }

    /// <summary>Base64-значення з останньої відповіді API; якщо задане — перевіряється конфлікт версій.</summary>
    public string? RowVersion { get; set; }
}

public sealed class StockChangeDto
{
    [Range(1, 100_000)]
    public int Quantity { get; set; }
}

public sealed class AssignCategoryDto
{
    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }

    public bool IsFeatured { get; set; }
}
