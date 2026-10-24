using MongoDB.Bson;
using Reviews.Domain.Common;
using Reviews.Domain.Enums;
using Reviews.Domain.Exceptions;
using Reviews.Domain.ValueObjects;

namespace Reviews.Domain.Entities;

/// <summary>
/// Відгук на товар — документ колекції reviews (Лаб. 01).
/// Вкладені: author (VO), comments[] (до 50), customization; посилання: productId, orderId, discussionId.
/// Стан змінюється лише через методи — кожен перевіряє бізнес-правила й оновлює updatedAt.
/// </summary>
public class Review : BaseEntity
{
    public const int MaxTitleLength = 120;
    public const int MinTextLength = 3;
    public const int MaxTextLength = 5000;
    public const int MaxComments = 50;                       // більше — у колекцію discussions

    // не readonly: ці поля заповнює BSON-десеріалізатор (BsonClassMap.MapField в Infrastructure)
    private List<Comment> _comments = new();
    private List<string> _pros = new();
    private List<string> _cons = new();
    private List<string> _photos = new();
    private Dictionary<string, object> _customization = new();

    public long ProductId { get; private set; }               // посилання на Каталог
    public string ProductName { get; private set; } = "";      // денормалізовано
    public ProductType? ProductType { get; private set; }
    public Author Author { get; private set; } = null!;
    public Rating Rating { get; private set; } = null!;
    public string? Title { get; private set; }
    public string Text { get; private set; } = "";
    public bool VerifiedPurchase { get; private set; }
    public long? OrderId { get; private set; }                 // посилання на Замовлення
    public int HelpfulVotes { get; private set; }
    public ObjectId? DiscussionId { get; private set; }

    public IReadOnlyCollection<Comment> Comments => _comments.AsReadOnly();
    public IReadOnlyCollection<string> Pros => _pros.AsReadOnly();
    public IReadOnlyCollection<string> Cons => _cons.AsReadOnly();
    public IReadOnlyCollection<string> Photos => _photos.AsReadOnly();
    public IReadOnlyDictionary<string, object> Customization => _customization.AsReadOnly();

    private Review() { }                                      // для десеріалізації

    public Review(long productId, string productName, ProductType? productType, Author author,
        Rating rating, string? title, string text,
        IEnumerable<string>? pros = null, IEnumerable<string>? cons = null,
        IReadOnlyDictionary<string, object>? customization = null, long? orderId = null)
    {
        if (productId <= 0)
            throw new DomainException("ProductId must be positive");
        if (string.IsNullOrWhiteSpace(productName))
            throw new DomainException("ProductName is required");

        ProductId = productId;
        ProductName = productName.Trim();
        ProductType = productType;
        Author = author ?? throw new DomainException("Author is required");
        SetContent(rating, title, text, pros, cons);

        if (customization is not null)
            foreach (var (key, value) in customization)
                _customization[key] = value;

        // відгук із замовленням магазину вважається підтвердженою покупкою
        OrderId = orderId;
        VerifiedPurchase = orderId is not null;
    }

    /// <summary>Редагування змісту відгуку автором.</summary>
    public void Update(Rating rating, string? title, string text,
        IEnumerable<string>? pros, IEnumerable<string>? cons)
    {
        SetContent(rating, title, text, pros, cons);
        Touch();
    }

    public void AddComment(string author, string text)
    {
        if (_comments.Count >= MaxComments)
            throw new DomainException(
                $"Review already has {MaxComments} comments; continue in a discussion");

        _comments.Add(new Comment(author, text, DateTime.UtcNow));
        Touch();
    }

    public void AddPhoto(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new DomainException("Photo url is required");
        if (!_photos.Contains(url))
            _photos.Add(url.Trim());
        Touch();
    }

    public void MarkHelpful()
    {
        HelpfulVotes++;
        Touch();
    }

    /// <summary>Довга розмова виноситься в discussions; відгук зберігає лише посилання.</summary>
    public void LinkDiscussion(ObjectId discussionId)
    {
        if (DiscussionId is not null && DiscussionId != discussionId)
            throw new DomainException("Review already has a discussion");
        DiscussionId = discussionId;
        Touch();
    }

    private void SetContent(Rating rating, string? title, string text,
        IEnumerable<string>? pros, IEnumerable<string>? cons)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < MinTextLength)
            throw new DomainException($"Review text must be at least {MinTextLength} characters");
        if (text.Length > MaxTextLength)
            throw new DomainException($"Review text must not exceed {MaxTextLength} characters");
        if (title?.Length > MaxTitleLength)
            throw new DomainException($"Review title must not exceed {MaxTitleLength} characters");

        Rating = rating ?? throw new DomainException("Rating is required");
        Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        Text = text.Trim();
        Replace(_pros, pros);
        Replace(_cons, cons);
    }

    private static void Replace(List<string> target, IEnumerable<string>? values)
    {
        target.Clear();
        if (values is null) return;
        target.AddRange(values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct());
    }
}
