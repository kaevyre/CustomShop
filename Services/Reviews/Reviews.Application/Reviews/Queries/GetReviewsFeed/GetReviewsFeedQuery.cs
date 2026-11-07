using FluentValidation;
using MediatR;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Models;
using Reviews.Application.Common.Validation;

namespace Reviews.Application.Reviews.Queries.GetReviewsFeed;

/// <summary>
/// Стрічка відгуків з курсорною пагінацією: After — Id останнього отриманого відгуку
/// (ObjectId зростає разом із часом створення, тому сортування за _id = «нові першими»).
/// </summary>
public sealed record GetReviewsFeedQuery(long? ProductId = null, string? After = null, int PageSize = 20)
    : IQuery<CursorPage<ReviewDto>>;

public sealed class GetReviewsFeedQueryHandler(IReviewQueries queries)
    : IRequestHandler<GetReviewsFeedQuery, CursorPage<ReviewDto>>
{
    public Task<CursorPage<ReviewDto>> Handle(GetReviewsFeedQuery q, CancellationToken ct)
        => queries.GetFeedAsync(q.ProductId, q.After, Math.Clamp(q.PageSize, 1, 100), ct);
}

public sealed class GetReviewsFeedQueryValidator : AbstractValidator<GetReviewsFeedQuery>
{
    public GetReviewsFeedQueryValidator()
    {
        RuleFor(x => x.After).MustBeObjectId().When(x => x.After is not null);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.ProductId).GreaterThan(0).When(x => x.ProductId is not null);
    }
}
