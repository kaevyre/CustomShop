namespace Reviews.Application.Reviews;

/// <summary>Рядок списку відгуків — лише поля, потрібні для картки (проєкція в БД).</summary>
public sealed record ReviewDto(
    string Id,
    long ProductId,
    string ProductName,
    string? ProductType,
    string AuthorProfileId,
    string AuthorName,
    int Rating,
    string? Title,
    string Text,
    bool VerifiedPurchase,
    int HelpfulVotes,
    int CommentsCount,
    DateTime CreatedAt,
    int Version);

public sealed record AuthorDto(string ProfileId, string DisplayName);

public sealed record CommentDto(string Author, string Text, DateTime CreatedAt);

/// <summary>Повний відгук: вкладені author, comments[], customization і масиви pros/cons/photos.</summary>
public sealed record ReviewDetailsDto
{
    public string Id { get; init; } = "";
    public long ProductId { get; init; }
    public string ProductName { get; init; } = "";
    public string? ProductType { get; init; }
    public AuthorDto Author { get; init; } = null!;
    public int Rating { get; init; }
    public string? Title { get; init; }
    public string Text { get; init; } = "";
    public IReadOnlyList<string> Pros { get; init; } = [];
    public IReadOnlyList<string> Cons { get; init; } = [];
    public IReadOnlyList<string> Photos { get; init; } = [];
    public IReadOnlyDictionary<string, object> Customization { get; init; } = new Dictionary<string, object>();
    public bool VerifiedPurchase { get; init; }
    public long? OrderId { get; init; }
    public int HelpfulVotes { get; init; }
    public IReadOnlyList<CommentDto> Comments { get; init; } = [];
    public string? DiscussionId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public int Version { get; init; }
}
