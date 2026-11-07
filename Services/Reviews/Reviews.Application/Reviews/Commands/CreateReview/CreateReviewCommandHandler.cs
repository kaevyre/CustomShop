using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Reviews.Domain.Entities;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Reviews.Commands.CreateReview;

public sealed class CreateReviewCommandHandler(IReviewRepository reviews, ILogger<CreateReviewCommandHandler> logger)
    : IRequestHandler<CreateReviewCommand, string>
{
    public async Task<string> Handle(CreateReviewCommand cmd, CancellationToken ct)
    {
        logger.LogInformation("CreateReviewCommandHandler: creating review of product {ProductId} by {AuthorProfileId}",
            cmd.ProductId, cmd.AuthorProfileId);

        var review = new Review(
            cmd.ProductId, cmd.ProductName, cmd.ProductType,
            new Author(ObjectId.Parse(cmd.AuthorProfileId), cmd.AuthorName),
            new Rating(cmd.Rating), cmd.Title, cmd.Text, cmd.Pros, cmd.Cons,
            cmd.Customization?.ToDictionary(p => p.Key, p => (object)p.Value),
            cmd.OrderId);

        await reviews.AddAsync(review, ct);
        return review.Id.ToString();
    }
}
