using FluentValidation;
using Reviews.Application.Common.Validation;
using Reviews.Domain.Entities;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Reviews.Commands.UpdateReview;

public sealed class UpdateReviewCommandValidator : AbstractValidator<UpdateReviewCommand>
{
    public UpdateReviewCommandValidator()
    {
        RuleFor(x => x.Id).MustBeObjectId();
        RuleFor(x => x.Rating).InclusiveBetween(Rating.Min, Rating.Max);
        RuleFor(x => x.Title).MaximumLength(Review.MaxTitleLength);
        RuleFor(x => x.Text).NotEmpty().Length(Review.MinTextLength, Review.MaxTextLength);
        RuleFor(x => x.Pros).MustBeShortList();
        RuleFor(x => x.Cons).MustBeShortList();
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0).When(x => x.ExpectedVersion is not null);
    }
}
