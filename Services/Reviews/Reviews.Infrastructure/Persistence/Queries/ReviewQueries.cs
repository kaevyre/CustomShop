using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Models;
using Reviews.Application.Reviews;
using Reviews.Infrastructure.Persistence.ReadModels;
using Reviews.Infrastructure.Persistence.Serialization;

namespace Reviews.Infrastructure.Persistence.Queries;

/// <summary>
/// Read-side відгуків: aggregation pipeline ($match → $sort → $skip/$limit → $project) над сирими документами;
/// з БД вибираються лише поля DTO, а commentsCount рахується на сервері ($size), без завантаження коментарів.
/// </summary>
public sealed class ReviewQueries(MongoDbContext context) : IReviewQueries
{
    private readonly IMongoCollection<BsonDocument> _reviews = context.ReviewDocuments;

    private static readonly BsonDocument ListProjection = new()
    {
        { "productId", 1 }, { "productName", 1 }, { "productType", 1 }, { "author", 1 },
        { "rating", 1 }, { "title", 1 }, { "text", 1 }, { "verifiedPurchase", 1 },
        { "helpfulVotes", 1 }, { "createdAt", 1 }, { "version", 1 },
        { "commentsCount", new BsonDocument("$size", new BsonDocument("$ifNull", new BsonArray { "$comments", new BsonArray() })) }
    };

    private static readonly BsonDocument DetailsProjection = new()
    {
        { "productId", 1 }, { "productName", 1 }, { "productType", 1 }, { "author", 1 },
        { "rating", 1 }, { "title", 1 }, { "text", 1 }, { "pros", 1 }, { "cons", 1 }, { "photos", 1 },
        { "customization", 1 }, { "verifiedPurchase", 1 }, { "orderId", 1 }, { "helpfulVotes", 1 },
        { "comments", 1 }, { "discussionId", 1 }, { "createdAt", 1 }, { "updatedAt", 1 }, { "version", 1 }
    };

    public async Task<ReviewDetailsDto?> GetByIdAsync(string id, CancellationToken ct)
    {
        if (!ObjectId.TryParse(id, out var objectId))
            return null;

        var row = await _reviews.Find(new BsonDocument("_id", objectId))
            .Project<ReviewDetailsRow>(DetailsProjection)
            .FirstOrDefaultAsync(ct);

        return row is null ? null : ToDetailsDto(row);
    }

    public async Task<PagedResult<ReviewDto>> GetListAsync(
        ReviewFilter filter, int page, int pageSize, CancellationToken ct)
    {
        // $facet: сторінка й загальна кількість одним запитом до сервера
        var stages = new[]
        {
            new BsonDocument("$match", BuildMatch(filter)),
            new BsonDocument("$facet", new BsonDocument
            {
                { "items", new BsonArray
                    {
                        new BsonDocument("$sort", new BsonDocument { { "createdAt", -1 }, { "_id", -1 } }),
                        new BsonDocument("$skip", (page - 1) * pageSize),
                        new BsonDocument("$limit", pageSize),
                        new BsonDocument("$project", ListProjection)
                    }
                },
                { "total", new BsonArray { new BsonDocument("$count", "count") } }
            })
        };

        var result = await _reviews
            .Aggregate(PipelineDefinition<BsonDocument, FacetPage<ReviewListRow>>.Create(stages), cancellationToken: ct)
            .FirstOrDefaultAsync(ct) ?? new FacetPage<ReviewListRow>();

        return new PagedResult<ReviewDto>(result.Items.Select(ToListDto).ToList(), page, pageSize, result.TotalCount);
    }

    public async Task<CursorPage<ReviewDto>> GetFeedAsync(
        long? productId, string? afterId, int pageSize, CancellationToken ct)
    {
        var match = new BsonDocument();
        if (productId is not null)
            match["productId"] = productId.Value;
        if (afterId is not null && ObjectId.TryParse(afterId, out var after))
            match["_id"] = new BsonDocument("$lt", after);          // курсор замість skip

        var stages = new[]
        {
            new BsonDocument("$match", match),
            new BsonDocument("$sort", new BsonDocument("_id", -1)),  // ObjectId зростає разом із часом створення
            new BsonDocument("$limit", pageSize + 1),               // +1 — щоб знати, чи є наступна сторінка
            new BsonDocument("$project", ListProjection)
        };

        var rows = await _reviews
            .Aggregate(PipelineDefinition<BsonDocument, ReviewListRow>.Create(stages), cancellationToken: ct)
            .ToListAsync(ct);

        var hasMore = rows.Count > pageSize;
        var items = rows.Take(pageSize).Select(ToListDto).ToList();
        return new CursorPage<ReviewDto>(items, hasMore ? items[^1].Id : null);
    }

    public async Task<ProductRatingDto?> GetProductRatingAsync(long productId, CancellationToken ct)
    {
        var stages = new[]
        {
            new BsonDocument("$match", new BsonDocument("productId", productId)),
            new BsonDocument("$group", new BsonDocument { { "_id", "$rating" }, { "count", new BsonDocument("$sum", 1) } }),
            new BsonDocument("$sort", new BsonDocument("_id", 1))
        };

        var buckets = await _reviews
            .Aggregate(PipelineDefinition<BsonDocument, RatingBucketRow>.Create(stages), cancellationToken: ct)
            .ToListAsync(ct);
        if (buckets.Count == 0)
            return null;

        var total = buckets.Sum(b => b.Count);
        var average = Math.Round(buckets.Sum(b => b.Id * b.Count) / (double)total, 2);
        return new ProductRatingDto(productId, total, average, buckets.ToDictionary(b => b.Id, b => b.Count));
    }

    private static BsonDocument BuildMatch(ReviewFilter filter)
    {
        var match = new BsonDocument();
        if (!string.IsNullOrWhiteSpace(filter.Search))                 // text-індекс tx_title_text
            match["$text"] = new BsonDocument("$search", filter.Search);
        if (filter.ProductId is not null)                             // ix_product_createdAt
            match["productId"] = filter.ProductId.Value;
        if (filter.AuthorProfileId is not null && ObjectId.TryParse(filter.AuthorProfileId, out var author))
            match["author.profileId"] = author;                       // ix_author_createdAt
        if (filter.MinRating is not null)
            match["rating"] = new BsonDocument("$gte", filter.MinRating.Value);
        return match;
    }

    private static ReviewDto ToListDto(ReviewListRow r) => new(
        r.Id.ToString(), r.ProductId, r.ProductName, r.ProductType,
        r.Author.ProfileId.ToString(), r.Author.DisplayName, r.Rating, r.Title, r.Text,
        r.VerifiedPurchase, r.HelpfulVotes, r.CommentsCount, r.CreatedAt, r.Version);

    private static ReviewDetailsDto ToDetailsDto(ReviewDetailsRow r) => new()
    {
        Id = r.Id.ToString(),
        ProductId = r.ProductId,
        ProductName = r.ProductName,
        ProductType = r.ProductType,
        Author = new AuthorDto(r.Author.ProfileId.ToString(), r.Author.DisplayName),
        Rating = r.Rating,
        Title = r.Title,
        Text = r.Text,
        Pros = r.Pros ?? [],
        Cons = r.Cons ?? [],
        Photos = r.Photos ?? [],
        Customization = CustomizationSerializer<Dictionary<string, object>>.ToDictionary(r.Customization),
        VerifiedPurchase = r.VerifiedPurchase,
        OrderId = r.OrderId,
        HelpfulVotes = r.HelpfulVotes,
        Comments = (r.Comments ?? []).Select(c => new CommentDto(c.Author, c.Text, c.CreatedAt)).ToList(),
        DiscussionId = r.DiscussionId?.ToString(),
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
        Version = r.Version
    };
}
