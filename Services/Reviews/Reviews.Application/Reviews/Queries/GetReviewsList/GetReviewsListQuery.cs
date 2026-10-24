using FluentValidation;
using MediatR;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Models;
using Reviews.Application.Common.Validation;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Reviews.Queries.GetReviewsList;

/// <summary>Список відгуків з фільтрами та пагінацією; фільтрація й skip/limit виконуються в MongoDB.</summary>
public sealed record GetReviewsListQuery(
    long? ProductId = null,
    string? AuthorProfileId = null,
    int? MinRating = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<ReviewDto>>;

public sealed class GetReviewsListQueryHandler(IReviewQueries queries)
    : IRequestHandler<GetReviewsListQuery, PagedResult<ReviewDto>>
{
    public const int MaxPageSize = 100;

    public Task<PagedResult<ReviewDto>> Handle(GetReviewsListQuery q, CancellationToken ct)
        => queries.GetListAsync(
            new ReviewFilter(q.ProductId, q.AuthorProfileId, q.MinRating, q.Search),
            q.Page, Math.Clamp(q.PageSize, 1, MaxPageSize), ct);
}

public sealed class GetReviewsListQueryValidator : AbstractValidator<GetReviewsListQuery>
{
    public GetReviewsListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, GetReviewsListQueryHandler.MaxPageSize);
        RuleFor(x => x.ProductId).GreaterThan(0).When(x => x.ProductId is not null);
        RuleFor(x => x.AuthorProfileId).MustBeObjectId().When(x => x.AuthorProfileId is not null);
        RuleFor(x => x.MinRating).InclusiveBetween(Rating.Min, Rating.Max).When(x => x.MinRating is not null);
        RuleFor(x => x.Search).MinimumLength(2).MaximumLength(100).When(x => x.Search is not null);
    }
}
