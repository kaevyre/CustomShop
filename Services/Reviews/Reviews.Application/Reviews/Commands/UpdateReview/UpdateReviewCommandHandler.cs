using AutoMapper;
using MediatR;
using MongoDB.Bson;
using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Reviews.Commands.UpdateReview;

public sealed class UpdateReviewCommandHandler(IReviewRepository reviews, IMapper mapper)
    : IRequestHandler<UpdateReviewCommand, ReviewDetailsDto>
{
    public async Task<ReviewDetailsDto> Handle(UpdateReviewCommand cmd, CancellationToken ct)
    {
        var review = await reviews.GetByIdAsync(ObjectId.Parse(cmd.Id), ct)
            ?? throw new NotFoundException(nameof(Review), cmd.Id);

        review.EnsureVersion(cmd.ExpectedVersion);
        review.Update(new Rating(cmd.Rating), cmd.Title, cmd.Text, cmd.Pros, cmd.Cons);

        await reviews.UpdateAsync(review, ct);
        return mapper.Map<ReviewDetailsDto>(review);
    }
}
