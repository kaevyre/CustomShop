using Catalog.Domain.Exceptions;

namespace Catalog.Domain.Entities;

public class Category : AuditableEntity
{
    private readonly List<ProductCategory> _productCategories = new();

    private Category()
    {
    }

    public Category(string name, string slug, int sortOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Category name is required.");
        if (!CatalogRules.IsValidSlug(slug))
            throw new DomainException("Category slug may contain only a-z, 0-9 and '-'.");

        Name = name.Trim();
        Slug = slug;
        SortOrder = sortOrder;
    }

    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }

    public IReadOnlyCollection<ProductCategory> ProductCategories => _productCategories;

    public void Delete() => MarkDeleted();
}
