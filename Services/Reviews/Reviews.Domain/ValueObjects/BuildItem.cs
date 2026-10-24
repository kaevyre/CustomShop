using Reviews.Domain.Common;
using Reviews.Domain.Enums;
using Reviews.Domain.Exceptions;

namespace Reviews.Domain.ValueObjects;

/// <summary>
/// Товар сетапу (елемент масиву builds.items): productId — посилання на Каталог без FK,
/// productName і productType — знімок на момент публікації; customization — налаштування,
/// у кожного типу товару свої ключі (гнучка схема), значення — рядки, числа або bool.
/// </summary>
public sealed class BuildItem : ValueObject
{
    public long ProductId { get; }
    public string ProductName { get; }
    public ProductType ProductType { get; }
    public IReadOnlyDictionary<string, object> Customization { get; }
    public string? Note { get; }

    public BuildItem(long productId, string productName, ProductType productType,
        IReadOnlyDictionary<string, object>? customization = null, string? note = null)
    {
        if (productId <= 0)
            throw new DomainException("ProductId must be positive");
        if (string.IsNullOrWhiteSpace(productName))
            throw new DomainException("ProductName is required");

        ProductId = productId;
        ProductName = productName.Trim();
        ProductType = productType;
        Customization = new Dictionary<string, object>(customization ?? new Dictionary<string, object>());
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ProductId;
        yield return ProductName;
        yield return ProductType;
        yield return Note;
        foreach (var (key, value) in Customization.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            yield return key;
            yield return value;
        }
    }
}
