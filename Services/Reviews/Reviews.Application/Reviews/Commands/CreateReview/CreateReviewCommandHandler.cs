using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Reviews.Application.Caching;
using Reviews.Application.Common.Telemetry;
using Reviews.Domain.Entities;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Reviews.Commands.CreateReview;

public sealed class CreateReviewCommandHandler(
    IReviewRepository reviews, ReviewsCacheInvalidator cacheInvalidator, ILogger<CreateReviewCommandHandler> logger)
    : IRequestHandler<CreateReviewCommand, string>
{
    public async Task<string> Handle(CreateReviewCommand cmd, CancellationToken ct)
    {
        using var activity = ReviewsTelemetry.Source.StartActivity("CreateReview");
        activity?.SetTag("product.id", cmd.ProductId);
        activity?.SetTag("review.rating", cmd.Rating);

        logger.LogInformation("CreateReviewCommandHandler: creating review of product {ProductId} by {AuthorProfileId}",
            cmd.ProductId, cmd.AuthorProfileId);

        var review = new Review(
            cmd.ProductId, cmd.ProductName, cmd.ProductType,
            new Author(ObjectId.Parse(cmd.AuthorProfileId), cmd.AuthorName),
            new Rating(cmd.Rating), cmd.Title, cmd.Text, cmd.Pros, cmd.Cons,
            cmd.Customization?.ToDictionary(p => p.Key, p => (object)p.Value),
            cmd.OrderId);

        await reviews.AddAsync(review, ct);
        await cacheInvalidator.ProductReviewsChangedAsync(cmd.ProductId, "ReviewCreated");

        var reviewId = review.Id.ToString();
        activity?.SetTag("review.id", reviewId);
        logger.LogInformation("Review {ReviewId} created for product {ProductId} with rating {Rating}, order {OrderId}",
            reviewId, cmd.ProductId, cmd.Rating, cmd.OrderId);
        return reviewId;
    }
}
