using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Reviews.Application.Common.Telemetry;
using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Reviews.Commands.UpdateReview;

public sealed class UpdateReviewCommandHandler(
    IReviewRepository reviews, IMapper mapper, ILogger<UpdateReviewCommandHandler> logger)
    : IRequestHandler<UpdateReviewCommand, ReviewDetailsDto>
{
    public async Task<ReviewDetailsDto> Handle(UpdateReviewCommand cmd, CancellationToken ct)
    {
        using var activity = ReviewsTelemetry.Source.StartActivity("UpdateReview");
        activity?.SetTag("review.id", cmd.Id);

        var review = await reviews.GetByIdAsync(ObjectId.Parse(cmd.Id), ct)
            ?? throw new NotFoundException(nameof(Review), cmd.Id);

        review.EnsureVersion(cmd.ExpectedVersion);
        review.Update(new Rating(cmd.Rating), cmd.Title, cmd.Text, cmd.Pros, cmd.Cons);

        await reviews.UpdateAsync(review, ct);
        logger.LogInformation("Review {ReviewId} of product {ProductId} updated: rating {Rating}",
            cmd.Id, review.ProductId, cmd.Rating);
        return mapper.Map<ReviewDetailsDto>(review);
    }
}
