using MongoDB.Bson;

namespace Reviews.Infrastructure.Persistence.ReadModels;

// Read-моделі — точна форма результату $project; DTO з Application з них будуються без зайвих полів.
// Імена полів camelCase за конвенцією з MongoMappings.

internal sealed class AuthorRow
{
    public ObjectId ProfileId { get; set; }
    public string DisplayName { get; set; } = "";
}

internal sealed class CommentRow
{
    public string Author { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

internal sealed class ReviewListRow
{
    public ObjectId Id { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string? ProductType { get; set; }
    public AuthorRow Author { get; set; } = new();
    public int Rating { get; set; }
    public string? Title { get; set; }
    public string Text { get; set; } = "";
    public bool VerifiedPurchase { get; set; }
    public int HelpfulVotes { get; set; }
    public int CommentsCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public int Version { get; set; }
}

internal sealed class ReviewDetailsRow
{
    public ObjectId Id { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string? ProductType { get; set; }
    public AuthorRow Author { get; set; } = new();
    public int Rating { get; set; }
    public string? Title { get; set; }
    public string Text { get; set; } = "";
    public List<string>? Pros { get; set; }
    public List<string>? Cons { get; set; }
    public List<string>? Photos { get; set; }
    public BsonDocument? Customization { get; set; }
    public bool VerifiedPurchase { get; set; }
    public long? OrderId { get; set; }
    public int HelpfulVotes { get; set; }
    public List<CommentRow>? Comments { get; set; }
    public ObjectId? DiscussionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int Version { get; set; }
}

internal sealed class RatingBucketRow
{
    public int Id { get; set; }                               // _id після $group — значення rating
    public int Count { get; set; }
}

internal sealed class BuildListRow
{
    public ObjectId Id { get; set; }
    public string Title { get; set; } = "";
    public AuthorRow Author { get; set; } = new();
    public int ItemsCount { get; set; }
    public List<string>? Tags { get; set; }
    public int Likes { get; set; }
    public DateTime CreatedAt { get; set; }
    public int Version { get; set; }
}

internal sealed class BuildItemRow
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductType { get; set; } = "";
    public BsonDocument? Customization { get; set; }
    public string? Note { get; set; }
}

internal sealed class BuildDetailsRow
{
    public ObjectId Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public AuthorRow Author { get; set; } = new();
    public List<BuildItemRow>? Items { get; set; }
    public List<string>? Tags { get; set; }
    public List<string>? Photos { get; set; }
    public int Likes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int Version { get; set; }
}

/// <summary>Результат $facet: сторінка документів і загальна кількість одним запитом.</summary>
internal sealed class FacetPage<TRow>
{
    public List<TRow> Items { get; set; } = [];
    public List<CountRow> Total { get; set; } = [];
    public long TotalCount => Total.FirstOrDefault()?.Count ?? 0;
}

internal sealed class CountRow
{
    public long Count { get; set; }
}
