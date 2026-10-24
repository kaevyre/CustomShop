using System.ComponentModel.DataAnnotations;

namespace Catalog.Bll.Dtos;

public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class CategoryWithProductsDto : CategoryDto
{
    public List<CategoryProductDto> Products { get; set; } = new();
}

/// <summary>Товар у категорії разом із власними полями зв'язку M:N.</summary>
public sealed class CategoryProductDto
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSku { get; set; } = string.Empty;
    public string? ProductBrandName { get; set; }
    public decimal ProductBasePrice { get; set; }
    public DateTime AddedAt { get; set; }
    public bool IsFeatured { get; set; }
}

public sealed class CreateCategoryDto
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Slug { get; set; } = string.Empty;

    [Range(0, 1000)]
    public int SortOrder { get; set; }
}

public sealed class BrandDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Country { get; set; }
    public string? Website { get; set; }
}
