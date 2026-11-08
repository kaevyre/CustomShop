using FluentValidation;
using MediatR;
using Reviews.Application.Caching;
using Reviews.Application.Common.Interfaces;
using Reviews.Domain.Exceptions;

namespace Reviews.Application.Reviews.Queries.GetProductRating;

/// <summary>Середня оцінка й розподіл оцінок товару (aggregation pipeline: $match → $group).</summary>
public sealed record GetProductRatingQuery(long ProductId) : IQuery<ProductRatingDto>, ICacheableQuery
{
    public string GetCacheKey(string listsVersion) => ReviewsCacheKeys.ProductRating(ProductId);

    public CacheProfile GetCacheProfile() => ReviewsCacheProfiles.Rating;
}

public sealed class GetProductRatingQueryHandler(IReviewQueries queries)
    : IRequestHandler<GetProductRatingQuery, ProductRatingDto>
{
    public async Task<ProductRatingDto> Handle(GetProductRatingQuery q, CancellationToken ct)
        => await queries.GetProductRatingAsync(q.ProductId, ct)
           ?? throw new NotFoundException("Product reviews", q.ProductId.ToString());
}

public sealed class GetProductRatingQueryValidator : AbstractValidator<GetProductRatingQuery>
{
    public GetProductRatingQueryValidator() => RuleFor(x => x.ProductId).GreaterThan(0);
}
