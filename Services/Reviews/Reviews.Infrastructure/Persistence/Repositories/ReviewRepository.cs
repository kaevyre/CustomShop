using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Domain.Entities;
using Reviews.Domain.Interfaces;

namespace Reviews.Infrastructure.Persistence.Repositories;

public sealed class ReviewRepository(MongoDbContext context)
    : MongoRepositoryBase<Review>(context.Reviews), IReviewRepository
{
    public Task<bool> ExistsAsync(long productId, ObjectId authorProfileId, CancellationToken ct = default)
    {
        // покривається унікальним індексом ux_product_author { productId: 1, "author.profileId": 1 }
        var filter = Builders<Review>.Filter.Eq(r => r.ProductId, productId)
                     & Builders<Review>.Filter.Eq("author.profileId", authorProfileId);
        return Collection.Find(filter).AnyAsync(ct);
    }
}
