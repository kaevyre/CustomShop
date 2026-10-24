using Reviews.Domain.Common;
using Reviews.Domain.Exceptions;
using Reviews.Domain.ValueObjects;

namespace Reviews.Domain.Entities;

/// <summary>
/// Сетап користувача — документ колекції builds (Лаб. 01): масив items[] із товарами та їх налаштуваннями,
/// tags[] і photos[]; агрегат читається й зберігається цілком.
/// </summary>
public class Build : BaseEntity
{
    public const int MinTitleLength = 3;
    public const int MaxTitleLength = 120;
    public const int MaxItems = 20;

    private List<BuildItem> _items = new();
    private List<string> _tags = new();
    private List<string> _photos = new();

    public string Title { get; private set; } = "";
    public string? Description { get; private set; }
    public Author Author { get; private set; } = null!;
    public int Likes { get; private set; }

    public IReadOnlyCollection<BuildItem> Items => _items.AsReadOnly();
    public IReadOnlyCollection<string> Tags => _tags.AsReadOnly();
    public IReadOnlyCollection<string> Photos => _photos.AsReadOnly();

    private Build() { }                                      // для десеріалізації

    public Build(string title, string? description, Author author,
        IEnumerable<BuildItem> items, IEnumerable<string>? tags = null)
    {
        Author = author ?? throw new DomainException("Author is required");
        SetDetails(title, description, tags);

        foreach (var item in items ?? [])
            AddItemInternal(item);
        if (_items.Count == 0)
            throw new DomainException("Build must contain at least one item");
    }

    public void UpdateDetails(string title, string? description, IEnumerable<string>? tags)
    {
        SetDetails(title, description, tags);
        Touch();
    }

    public void AddItem(BuildItem item)
    {
        AddItemInternal(item);
        Touch();
    }

    public void RemoveItem(long productId)
    {
        var item = _items.FirstOrDefault(i => i.ProductId == productId)
            ?? throw new DomainException($"Build has no item with productId {productId}");
        if (_items.Count == 1)
            throw new DomainException("Build must contain at least one item");

        _items.Remove(item);
        Touch();
    }

    public void Like()
    {
        Likes++;
        Touch();
    }

    private void AddItemInternal(BuildItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (_items.Count >= MaxItems)
            throw new DomainException($"Build cannot contain more than {MaxItems} items");
        if (_items.Any(i => i.ProductId == item.ProductId))
            throw new DomainException($"Product {item.ProductId} is already in the build");
        _items.Add(item);
    }

    private void SetDetails(string title, string? description, IEnumerable<string>? tags)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length is < MinTitleLength or > MaxTitleLength)
            throw new DomainException($"Build title must be {MinTitleLength}-{MaxTitleLength} characters");

        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        _tags.Clear();
        if (tags is not null)
            _tags.AddRange(tags.Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim().ToLowerInvariant()).Distinct());
    }
}
