using FluentValidation;
using MongoDB.Bson;
using Reviews.Application.Common.Validation;
using Reviews.Domain.Entities;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Reviews.Commands.CreateReview;

public sealed class CreateReviewCommandValidator : AbstractValidator<CreateReviewCommand>
{
    public CreateReviewCommandValidator(IReviewRepository reviews)
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.ProductName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ProductType).IsInEnum();
        RuleFor(x => x.AuthorProfileId).MustBeObjectId();
        RuleFor(x => x.AuthorName).NotEmpty().MaximumLength(Author.MaxDisplayNameLength);
        RuleFor(x => x.Rating).InclusiveBetween(Rating.Min, Rating.Max);
        RuleFor(x => x.Title).MaximumLength(Review.MaxTitleLength);
        RuleFor(x => x.Text).NotEmpty().Length(Review.MinTextLength, Review.MaxTextLength);
        RuleFor(x => x.Pros).MustBeShortList();
        RuleFor(x => x.Cons).MustBeShortList();
        RuleFor(x => x.Customization).MustBeValidSubdocument();
        RuleFor(x => x.OrderId).GreaterThan(0).When(x => x.OrderId is not null);

        // Асинхронне правило із запитом до MongoDB: один автор — один відгук на товар.
        // Унікальний індекс ux_product_author страхує від гонки двох одночасних запитів (→ 409).
        RuleFor(x => x)
            .MustAsync(async (cmd, ct) =>
                !await reviews.ExistsAsync(cmd.ProductId, ObjectId.Parse(cmd.AuthorProfileId), ct))
            .WithName("AuthorProfileId")
            .WithMessage("You have already reviewed this product")
            .When(x => x.ProductId > 0 && ValidationRules.IsObjectId(x.AuthorProfileId));
    }
}
