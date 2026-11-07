using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Application.Builds;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Models;
using Reviews.Application.Reviews;
using Reviews.Infrastructure.Persistence.ReadModels;
using Reviews.Infrastructure.Persistence.Serialization;

namespace Reviews.Infrastructure.Persistence.Queries;

/// <summary>Read-side сетапів: фільтр за тегом (ix_tags_likes) або товаром (ix_items_productId), популярні першими.</summary>
public sealed class BuildQueries(MongoDbContext context) : IBuildQueries
{
    private readonly IMongoCollection<BsonDocument> _builds = context.BuildDocuments;

    private static readonly BsonDocument ListProjection = new()
    {
        { "title", 1 }, { "author", 1 }, { "tags", 1 }, { "likes", 1 }, { "createdAt", 1 }, { "version", 1 },
        { "itemsCount", new BsonDocument("$size", new BsonDocument("$ifNull", new BsonArray { "$items", new BsonArray() })) }
    };

    public async Task<BuildDetailsDto?> GetByIdAsync(string id, CancellationToken ct)
    {
        if (!ObjectId.TryParse(id, out var objectId))
            return null;

        var row = await _builds.Find(new BsonDocument("_id", objectId))
            .Project<BuildDetailsRow>(Builders<BsonDocument>.Projection
                .Include("title").Include("description").Include("author").Include("items").Include("tags")
                .Include("photos").Include("likes").Include("createdAt").Include("updatedAt").Include("version"))
            .FirstOrDefaultAsync(ct);

        return row is null ? null : new BuildDetailsDto
        {
            Id = row.Id.ToString(),
            Title = row.Title,
            Description = row.Description,
            Author = new AuthorDto(row.Author.ProfileId.ToString(), row.Author.DisplayName),
            Items = (row.Items ?? []).Select(i => new BuildItemDto(i.ProductId, i.ProductName, i.ProductType,
                CustomizationSerializer<Dictionary<string, object>>.ToDictionary(i.Customization), i.Note)).ToList(),
            Tags = row.Tags ?? [],
            Photos = row.Photos ?? [],
            Likes = row.Likes,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt,
            Version = row.Version
        };
    }

    public async Task<PagedResult<BuildDto>> GetListAsync(
        string? tag, long? productId, int page, int pageSize, CancellationToken ct)
    {
        var match = new BsonDocument();
        if (!string.IsNullOrWhiteSpace(tag))
            match["tags"] = tag.Trim().ToLowerInvariant();
        if (productId is not null)
            match["items.productId"] = productId.Value;

        var stages = new[]
        {
            new BsonDocument("$match", match),
            new BsonDocument("$facet", new BsonDocument
            {
                { "items", new BsonArray
                    {
                        new BsonDocument("$sort", new BsonDocument { { "likes", -1 }, { "_id", -1 } }),
                        new BsonDocument("$skip", (page - 1) * pageSize),
                        new BsonDocument("$limit", pageSize),
                        new BsonDocument("$project", ListProjection)
                    }
                },
                { "total", new BsonArray { new BsonDocument("$count", "count") } }
            })
        };

        var result = await _builds
            .Aggregate(PipelineDefinition<BsonDocument, FacetPage<BuildListRow>>.Create(stages), cancellationToken: ct)
            .FirstOrDefaultAsync(ct) ?? new FacetPage<BuildListRow>();

        var items = result.Items.Select(b => new BuildDto(b.Id.ToString(), b.Title, b.Author.DisplayName,
            b.ItemsCount, b.Tags ?? [], b.Likes, b.CreatedAt, b.Version)).ToList();
        return new PagedResult<BuildDto>(items, page, pageSize, result.TotalCount);
    }
}
