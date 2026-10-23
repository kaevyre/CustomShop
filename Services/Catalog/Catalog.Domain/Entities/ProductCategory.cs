using Catalog.Domain.Exceptions;

namespace Catalog.Domain.Entities;

/// <summary>
/// Явна проміжна сутність M:N Product ↔ Category з власними полями AddedAt і IsFeatured
/// (автоматичне M:N в EF Core таких полів зберегти не дозволило б).
/// </summary>
public class ProductCategory
{
    private ProductCategory()
    {
    }

    public ProductCategory(long productId, int categoryId, DateTime addedAt, bool isFeatured = false)
    {
        if (categoryId <= 0)
            throw new DomainException("Category is required.");

        ProductId = productId;
        CategoryId = categoryId;
        AddedAt = addedAt;
        IsFeatured = isFeatured;
    }

    public long ProductId { get; private set; }
    public Product Product { get; private set; } = null!;
    public int CategoryId { get; private set; }
    public Category Category { get; private set; } = null!;
    public DateTime AddedAt { get; private set; }
    public bool IsFeatured { get; private set; }
}
