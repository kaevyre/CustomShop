using System.ComponentModel.DataAnnotations;

namespace Catalog.Bll.Dtos;

/// <summary>Параметри списку товарів: пагінація, фільтри й сортування лише за дозволеними полями.</summary>
public sealed class ProductQuery : PageRequest
{
    public static readonly IReadOnlyList<string> SortFields = ["name", "price", "stock", "created"];

    [StringLength(100)]
    public string? Search { get; set; }

    public int? BrandId { get; set; }
    public int? CategoryId { get; set; }
    public string? ProductType { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool? IsCustomizable { get; set; }

    public string SortBy { get; set; } = "name";
    public string SortDir { get; set; } = "asc";
}
