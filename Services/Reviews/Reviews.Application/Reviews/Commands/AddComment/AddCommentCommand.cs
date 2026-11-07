using AutoMapper;
using FluentValidation;
using MediatR;
using MongoDB.Bson;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Validation;
using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;

namespace Reviews.Application.Reviews.Commands.AddComment;

/// <summary>Додає короткий коментар у вкладений масив comments (до 50 у документі).</summary>
public sealed record AddCommentCommand(string ReviewId, string Author, string Text) : ICommand<ReviewDetailsDto>;

public sealed class AddCommentCommandHandler(IReviewRepository reviews, IMapper mapper)
    : IRequestHandler<AddCommentCommand, ReviewDetailsDto>
{
    public async Task<ReviewDetailsDto> Handle(AddCommentCommand cmd, CancellationToken ct)
    {
        var review = await reviews.GetByIdAsync(ObjectId.Parse(cmd.ReviewId), ct)
            ?? throw new NotFoundException(nameof(Review), cmd.ReviewId);

        review.AddComment(cmd.Author, cmd.Text);
        await reviews.UpdateAsync(review, ct);
        return mapper.Map<ReviewDetailsDto>(review);
    }
}

public sealed class AddCommentCommandValidator : AbstractValidator<AddCommentCommand>
{
    public AddCommentCommandValidator()
    {
        RuleFor(x => x.ReviewId).MustBeObjectId();
        RuleFor(x => x.Author).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Text).NotEmpty().MaximumLength(Comment.MaxTextLength);
    }
}
