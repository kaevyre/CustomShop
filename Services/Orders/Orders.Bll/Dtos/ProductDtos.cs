using System.ComponentModel.DataAnnotations;

namespace Orders.Bll.Dtos;

public sealed class ProductDto
{
    public long Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public bool IsActive { get; set; }
    public DateTime SyncedAt { get; set; }
    public string? RowVersion { get; set; }
}

public class UpdateProductDto
{
    [Required, StringLength(50, MinimumLength = 1)]
    public string Sku { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "999999999999999", ParseLimitsInInvariantCulture = true)]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int Stock { get; set; }

    public bool IsActive { get; set; } = true;

    public string? RowVersion { get; set; }
}

public sealed class CreateProductDto : UpdateProductDto
{
    [Range(1, long.MaxValue)]
    public long Id { get; set; }
}
